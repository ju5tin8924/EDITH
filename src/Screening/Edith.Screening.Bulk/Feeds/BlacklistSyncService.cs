using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using Edith.Screening.Core.Models;
using Edith.Screening.Core.Search;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Edith.Screening.Bulk.Feeds;

public interface IBlacklistSyncService
{
    Task<IReadOnlyList<BlacklistSourceConfig>> GetSourcesAsync(CancellationToken ct = default);
    Task<BlacklistSourceConfig> UpdateSourceAsync(string sourceType, string url, bool isEnabled, string updatedBy, CancellationToken ct = default);
    UrlValidationResultDto ValidateUrl(string url);
    Task<SourceTestResultDto> TestSourceConnectivityAsync(string sourceType, string? overrideUrl = null, CancellationToken ct = default);
    Task<BlacklistScheduleConfig> GetScheduleConfigAsync(CancellationToken ct = default);
    Task<BlacklistScheduleConfig> UpdateScheduleConfigAsync(bool isScheduleEnabled, string dailyExecutionTimeUtc, int retentionDays, string updatedBy, CancellationToken ct = default);
    Task<BlacklistLiveStatusDto> GetLiveStatusAsync(CancellationToken ct = default);
    Task<(bool Accepted, string Message, BlacklistSyncRun? Run)> TriggerSyncAsync(string triggerMode, string triggeredBy, IReadOnlyList<string>? selectedSources = null, bool waitForCompletion = false, CancellationToken ct = default);
    Task<IReadOnlyList<BlacklistSyncRun>> GetRunsAsync(int maxResults = 30, CancellationToken ct = default);
    Task<BlacklistSyncRun?> GetRunByIdAsync(Guid runId, CancellationToken ct = default);
    Task<PaginatedSyncItemsResult> GetRunItemsAsync(Guid runId, string? action = null, string? sourceType = null, string? search = null, string? reasonFilter = null, string sortBy = "id", string sortDir = "asc", int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<IReadOnlyList<BlacklistSyncError>> GetRunErrorsAsync(Guid runId, CancellationToken ct = default);
    Task<BlacklistDistributionDto> GetDistributionStatsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<BlacklistDailyTrendPoint>> GetDailyTrendStatsAsync(DateOnly? startDate = null, DateOnly? endDate = null, string? sourceType = null, CancellationToken ct = default);
}

public sealed class BlacklistSyncService : IBlacklistSyncService, IDisposable
{
    private const long AdvisoryLockKey = 0x4544_4954_4800_0002L;

    private readonly string? _connectionString;
    private readonly HttpClient _httpClient;
    private readonly IWatchlistSearchEngine? _searchEngine;
    private readonly InMemoryWatchlistSearchEngine? _inMemorySearchEngine;
    private readonly ILogger<BlacklistSyncService>? _logger;
    private readonly SemaphoreSlim _executionGate = new(1, 1);

    private readonly ConcurrentDictionary<string, BlacklistSourceConfig> _memSources = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, BlacklistSyncRun> _memRuns = new();
    private readonly ConcurrentDictionary<Guid, List<BlacklistSyncItem>> _memItems = new();
    private readonly ConcurrentDictionary<string, StoredWatchlistSnapshot> _memEntities = new(StringComparer.OrdinalIgnoreCase);
    private BlacklistScheduleConfig _memSchedule = new()
    {
        IsScheduleEnabled = false,
        DailyExecutionTimeUtc = "02:00",
        Timezone = "UTC",
        RetentionDays = 90
    };

    private volatile BlacklistSyncRun? _activeRun;

    public BlacklistSyncService(
        string? connectionString = null,
        HttpClient? httpClient = null,
        IWatchlistSearchEngine? searchEngine = null,
        InMemoryWatchlistSearchEngine? inMemorySearchEngine = null,
        ILogger<BlacklistSyncService>? logger = null)
    {
        _connectionString = connectionString;
        _httpClient = httpClient ?? CreateDefaultHttpClient();
        _searchEngine = searchEngine;
        _inMemorySearchEngine = inMemorySearchEngine;
        _logger = logger;

        foreach (var src in BlacklistSourceDefaults.CreateDefaultSources())
        {
            _memSources[src.SourceType] = src;
        }
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(30)
        };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("EDITH-AML-Compliance-Engine/2.7 (+https://edith.local)");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/xml, text/xml, */*");
        return client;
    }

    private bool HasDatabase => !string.IsNullOrWhiteSpace(_connectionString);

    public UrlValidationResultDto ValidateUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return new UrlValidationResultDto(false, string.Empty, "Please enter a valid HTTP, HTTPS, or file URL for the blacklist source.");
        }

        var trimmed = url.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeFile))
        {
            return new UrlValidationResultDto(false, trimmed, "The URL must be a valid absolute HTTP, HTTPS, or FILE address (e.g., https://... or file:///...).");
        }

        if (uri.Scheme != Uri.UriSchemeFile && (string.IsNullOrWhiteSpace(uri.Host) || uri.Host.Contains(' ')))
        {
            return new UrlValidationResultDto(false, trimmed, "The URL host name is invalid.");
        }

        return new UrlValidationResultDto(true, uri.ToString(), "URL format is valid.");
    }

    public async Task<IReadOnlyList<BlacklistSourceConfig>> GetSourcesAsync(CancellationToken ct = default)
    {
        if (HasDatabase)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(ct);

                const string sql = """
                    SELECT s.source_type, s.source_ref_id, s.display_name, s.url, s.is_enabled,
                           s.last_successful_sync_at, s.last_attempt_at, s.last_status, s.last_error_user_message,
                           COALESCE(cnt.active_cnt, s.active_record_count) AS active_cnt,
                           s.updated_by, s.updated_at
                    FROM screening.blacklist_sources s
                    LEFT JOIN (
                        SELECT source_type, COUNT(*)::int AS active_cnt
                        FROM screening.watchlist_entities
                        WHERE is_active = true AND source_type IS NOT NULL
                        GROUP BY source_type
                    ) cnt ON cnt.source_type = s.source_type
                    ORDER BY CASE s.source_type
                        WHEN 'OFAC' THEN 1
                        WHEN 'UN' THEN 2
                        WHEN 'EU' THEN 3
                        WHEN 'UK' THEN 4
                        WHEN 'WORLD_CHECK' THEN 5
                        ELSE 6
                    END;
                    """;

                await using var cmd = new NpgsqlCommand(sql, conn);
                var list = new List<BlacklistSourceConfig>();
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    var cfg = new BlacklistSourceConfig
                    {
                        SourceType = r.GetString(0),
                        SourceRefId = r.GetString(1),
                        DisplayName = r.GetString(2),
                        Url = r.GetString(3),
                        IsEnabled = r.GetBoolean(4),
                        LastSuccessfulSyncAt = r.IsDBNull(5) ? null : r.GetFieldValue<DateTimeOffset>(5),
                        LastAttemptAt = r.IsDBNull(6) ? null : r.GetFieldValue<DateTimeOffset>(6),
                        LastStatus = r.GetString(7),
                        LastErrorUserMessage = r.IsDBNull(8) ? null : r.GetString(8),
                        ActiveRecordCount = r.GetInt32(9),
                        UpdatedBy = r.IsDBNull(10) ? null : r.GetString(10),
                        UpdatedAt = r.GetFieldValue<DateTimeOffset>(11)
                    };
                    list.Add(cfg);
                    _memSources[cfg.SourceType] = cfg;
                }

                if (list.Count > 0)
                {
                    return list;
                }
            }
            catch (Exception)
            {
                // Fallback to in-memory store
            }
        }

        foreach (var src in _memSources.Values)
        {
            src.ActiveRecordCount = _memEntities.Values.Count(e => e.IsActive && string.Equals(e.Record.SourceType, src.SourceType, StringComparison.OrdinalIgnoreCase));
        }

        return _memSources.Values
            .OrderBy(s => s.SourceType switch { "OFAC" => 1, "UN" => 2, "EU" => 3, "UK" => 4, "WORLD_CHECK" => 5, _ => 6 })
            .ToList();
    }

    public async Task<BlacklistSourceConfig> UpdateSourceAsync(string sourceType, string url, bool isEnabled, string updatedBy, CancellationToken ct = default)
    {
        var key = sourceType.Trim().ToUpperInvariant();
        var validation = ValidateUrl(url);
        if (!validation.IsValid)
        {
            throw new ArgumentException(validation.UserMessage, nameof(url));
        }

        if (!_memSources.TryGetValue(key, out var existing))
        {
            existing = BlacklistSourceDefaults.CreateDefaultSources()
                .FirstOrDefault(s => string.Equals(s.SourceType, key, StringComparison.OrdinalIgnoreCase))
                ?? throw new KeyNotFoundException($"Unknown blacklist source type '{sourceType}'.");
        }

        existing.Url = validation.NormalizedUrl;
        existing.IsEnabled = isEnabled;
        existing.UpdatedBy = updatedBy;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        _memSources[key] = existing;

        if (HasDatabase)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(ct);

                const string sql = """
                    UPDATE screening.blacklist_sources
                    SET url = @url,
                        is_enabled = @enabled,
                        updated_by = @by,
                        updated_at = CURRENT_TIMESTAMP
                    WHERE source_type = @src;
                    """;

                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("url", existing.Url);
                cmd.Parameters.AddWithValue("enabled", existing.IsEnabled);
                cmd.Parameters.AddWithValue("by", updatedBy);
                cmd.Parameters.AddWithValue("src", key);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (Exception)
            {
                // In-memory updated
            }
        }

        return existing;
    }

    public async Task<SourceTestResultDto> TestSourceConnectivityAsync(string sourceType, string? overrideUrl = null, CancellationToken ct = default)
    {
        var key = sourceType.Trim().ToUpperInvariant();
        var sources = await GetSourcesAsync(ct);
        var source = sources.FirstOrDefault(s => string.Equals(s.SourceType, key, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Unknown blacklist source '{sourceType}'.");

        var targetUrl = !string.IsNullOrWhiteSpace(overrideUrl) ? overrideUrl.Trim() : source.Url;
        var validation = ValidateUrl(targetUrl);
        if (!validation.IsValid)
        {
            return new SourceTestResultDto
            {
                SourceType = key,
                SourceRefId = source.SourceRefId,
                TestedUrl = targetUrl,
                IsReachable = false,
                IsXmlValid = false,
                HttpStatusCode = 0,
                LatencyMs = 0,
                UserMessage = validation.UserMessage,
                TechnicalDetails = "URL validation failed before network request."
            };
        }

        var sw = Stopwatch.StartNew();
        try
        {
            if (Uri.TryCreate(validation.NormalizedUrl, UriKind.Absolute, out var testUri) && testUri.Scheme == Uri.UriSchemeFile)
            {
                if (!File.Exists(testUri.LocalPath))
                {
                    sw.Stop();
                    return new SourceTestResultDto
                    {
                        SourceType = key,
                        SourceRefId = source.SourceRefId,
                        TestedUrl = validation.NormalizedUrl,
                        IsReachable = false,
                        IsXmlValid = false,
                        HttpStatusCode = 404,
                        LatencyMs = sw.ElapsedMilliseconds,
                        UserMessage = $"Local feed file not found at '{testUri.LocalPath}'.",
                        TechnicalDetails = "File.Exists returned false."
                    };
                }

                await using var fileStream = File.OpenRead(testUri.LocalPath);
                var outcome = await SanctionsFeedParsers.ParseXmlStreamAsync(key, source.SourceRefId, fileStream, maxRecords: 5, ct: ct);
                sw.Stop();

                return new SourceTestResultDto
                {
                    SourceType = key,
                    SourceRefId = source.SourceRefId,
                    TestedUrl = validation.NormalizedUrl,
                    IsReachable = true,
                    IsXmlValid = true,
                    HttpStatusCode = 200,
                    LatencyMs = sw.ElapsedMilliseconds,
                    RootElementName = outcome.RootElementName,
                    SampleRecordCount = outcome.Records.Count,
                    SampleRecords = outcome.Records,
                    UserMessage = $"Connected to {key} feed successfully (File stream, root <{outcome.RootElementName}>, {outcome.Records.Count} sample record(s) verified in {sw.ElapsedMilliseconds} ms)."
                };
            }

            using var req = new HttpRequestMessage(HttpMethod.Get, validation.NormalizedUrl);
            using var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            var statusCode = (int)resp.StatusCode;

            if (!resp.IsSuccessStatusCode)
            {
                sw.Stop();
                var translated = BlacklistErrorTranslator.TranslateHttpStatus(key, resp.StatusCode);
                return new SourceTestResultDto
                {
                    SourceType = key,
                    SourceRefId = source.SourceRefId,
                    TestedUrl = validation.NormalizedUrl,
                    IsReachable = false,
                    IsXmlValid = false,
                    HttpStatusCode = statusCode,
                    LatencyMs = sw.ElapsedMilliseconds,
                    UserMessage = translated.UserMessage,
                    TechnicalDetails = translated.TechnicalDetails
                };
            }

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            var outcomeHttp = await SanctionsFeedParsers.ParseXmlStreamAsync(key, source.SourceRefId, stream, maxRecords: 5, ct: ct);
            sw.Stop();

            return new SourceTestResultDto
            {
                SourceType = key,
                SourceRefId = source.SourceRefId,
                TestedUrl = validation.NormalizedUrl,
                IsReachable = true,
                IsXmlValid = true,
                HttpStatusCode = statusCode,
                LatencyMs = sw.ElapsedMilliseconds,
                RootElementName = outcomeHttp.RootElementName,
                SampleRecordCount = outcomeHttp.Records.Count,
                SampleRecords = outcomeHttp.Records,
                UserMessage = $"Connected to {key} successfully (HTTP {statusCode}, root <{outcomeHttp.RootElementName}>, {outcomeHttp.Records.Count} sample record(s) verified in {sw.ElapsedMilliseconds} ms)."
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            var translated = BlacklistErrorTranslator.TranslateException(key, ex, "FETCH");
            return new SourceTestResultDto
            {
                SourceType = key,
                SourceRefId = source.SourceRefId,
                TestedUrl = validation.NormalizedUrl,
                IsReachable = ex is XmlException,
                IsXmlValid = false,
                HttpStatusCode = ex is HttpRequestException { StatusCode: not null } hre ? (int)hre.StatusCode.Value : 0,
                LatencyMs = sw.ElapsedMilliseconds,
                UserMessage = translated.UserMessage,
                TechnicalDetails = translated.TechnicalDetails
            };
        }
    }

    public async Task<BlacklistScheduleConfig> GetScheduleConfigAsync(CancellationToken ct = default)
    {
        if (HasDatabase)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(ct);

                const string sql = """
                    SELECT is_schedule_enabled, daily_execution_time_utc, timezone, retention_days,
                           last_scheduled_run_date, updated_by, updated_at
                    FROM screening.blacklist_sync_config
                    WHERE id = 1;
                    """;

                await using var cmd = new NpgsqlCommand(sql, conn);
                await using var r = await cmd.ExecuteReaderAsync(ct);
                if (await r.ReadAsync(ct))
                {
                    _memSchedule = new BlacklistScheduleConfig
                    {
                        IsScheduleEnabled = r.GetBoolean(0),
                        DailyExecutionTimeUtc = r.GetString(1),
                        Timezone = r.GetString(2),
                        RetentionDays = r.GetInt32(3),
                        LastScheduledRunDate = r.IsDBNull(4) ? null : r.GetFieldValue<DateOnly>(4),
                        UpdatedBy = r.IsDBNull(5) ? null : r.GetString(5),
                        UpdatedAt = r.GetFieldValue<DateTimeOffset>(6)
                    };
                }
            }
            catch (Exception)
            {
                // Fallback to _memSchedule
            }
        }

        return _memSchedule;
    }

    public async Task<BlacklistScheduleConfig> UpdateScheduleConfigAsync(
        bool isScheduleEnabled,
        string dailyExecutionTimeUtc,
        int retentionDays,
        string updatedBy,
        CancellationToken ct = default)
    {
        var normalizedTime = TimeOnly.TryParse(dailyExecutionTimeUtc?.Trim(), CultureInfo.InvariantCulture, out var parsedTime)
            ? $"{parsedTime.Hour:D2}:{parsedTime.Minute:D2}"
            : "02:00";

        var clampedRetention = Math.Clamp(retentionDays, 7, 365);

        _memSchedule.IsScheduleEnabled = isScheduleEnabled;
        _memSchedule.DailyExecutionTimeUtc = normalizedTime;
        _memSchedule.Timezone = "UTC";
        _memSchedule.RetentionDays = clampedRetention;
        _memSchedule.UpdatedBy = updatedBy;
        _memSchedule.UpdatedAt = DateTimeOffset.UtcNow;

        if (HasDatabase)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(ct);

                const string sql = """
                    UPDATE screening.blacklist_sync_config
                    SET is_schedule_enabled = @enabled,
                        daily_execution_time_utc = @time,
                        timezone = 'UTC',
                        retention_days = @retention,
                        updated_by = @by,
                        updated_at = CURRENT_TIMESTAMP
                    WHERE id = 1;
                    """;

                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("enabled", isScheduleEnabled);
                cmd.Parameters.AddWithValue("time", normalizedTime);
                cmd.Parameters.AddWithValue("retention", clampedRetention);
                cmd.Parameters.AddWithValue("by", updatedBy);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (Exception)
            {
                // Fallback updated in memory
            }
        }

        return _memSchedule;
    }

    public async Task<BlacklistLiveStatusDto> GetLiveStatusAsync(CancellationToken ct = default)
    {
        var sources = await GetSourcesAsync(ct);
        var config = await GetScheduleConfigAsync(ct);
        var active = _activeRun;

        BlacklistSyncRun? latestRun = active;
        if (latestRun is null)
        {
            var runs = await GetRunsAsync(1, ct);
            latestRun = runs.Count > 0 ? runs[0] : null;
        }

        return new BlacklistLiveStatusDto
        {
            IsRunning = active is not null && active.Status == "RUNNING",
            CurrentSource = active?.CurrentSource,
            ActiveOrLatestRun = latestRun,
            ScheduleConfig = config,
            Sources = sources
        };
    }

    public async Task<(bool Accepted, string Message, BlacklistSyncRun? Run)> TriggerSyncAsync(
        string triggerMode,
        string triggeredBy,
        IReadOnlyList<string>? selectedSources = null,
        bool waitForCompletion = false,
        CancellationToken ct = default)
    {
        if (!await _executionGate.WaitAsync(0, ct))
        {
            return (
                Accepted: false,
                Message: "A blacklist update is already in progress. Please wait for the current execution to complete before starting another update.",
                Run: _activeRun);
        }

        var run = new BlacklistSyncRun
        {
            Id = Guid.NewGuid(),
            TriggerMode = string.IsNullOrWhiteSpace(triggerMode) ? "MANUAL" : triggerMode.ToUpperInvariant(),
            TriggeredBy = string.IsNullOrWhiteSpace(triggeredBy) ? "System Administrator" : triggeredBy,
            StartedAt = DateTimeOffset.UtcNow,
            Status = "RUNNING"
        };

        _activeRun = run;
        _memRuns[run.Id] = run;
        _memItems[run.Id] = [];

        if (waitForCompletion)
        {
            try
            {
                await ExecuteSyncCoreAsync(run, selectedSources, ct);
                return (true, run.UserSummaryMessage ?? "Blacklist update completed.", run);
            }
            finally
            {
                _activeRun = null;
                _executionGate.Release();
            }
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await ExecuteSyncCoreAsync(run, selectedSources, CancellationToken.None);
            }
            finally
            {
                _activeRun = null;
                _executionGate.Release();
            }
        }, CancellationToken.None);

        return (true, "Daily blacklist update started.", run);
    }

    private async Task ExecuteSyncCoreAsync(
        BlacklistSyncRun run,
        IReadOnlyList<string>? selectedSources,
        CancellationToken ct)
    {
        var runSw = Stopwatch.StartNew();
        NpgsqlConnection? lockConn = null;

        try
        {
            if (HasDatabase)
            {
                try
                {
                    lockConn = new NpgsqlConnection(_connectionString);
                    await lockConn.OpenAsync(ct);
                    await using var lockCmd = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key);", lockConn);
                    lockCmd.Parameters.AddWithValue("key", AdvisoryLockKey);
                    var acquired = await lockCmd.ExecuteScalarAsync(ct) is true;
                    if (!acquired)
                    {
                        run.Status = "FAILED";
                        run.UserSummaryMessage = "Another application instance is already executing the daily blacklist update.";
                        run.CompletedAt = DateTimeOffset.UtcNow;
                        return;
                    }
                }
                catch (Exception)
                {
                    // Proceed with in-process lock if DB advisory lock connection fails
                }
            }

            await PersistRunHeaderAsync(run, isInsert: true, ct);

            var allSources = await GetSourcesAsync(ct);
            var targetSources = allSources
                .Where(s => s.IsEnabled)
                .Where(s => selectedSources is null || selectedSources.Count == 0 ||
                            selectedSources.Any(sel => string.Equals(sel, s.SourceType, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            run.SourcesProcessed = targetSources.Count;

            foreach (var source in targetSources)
            {
                run.CurrentSource = source.SourceType;
                await PersistRunHeaderAsync(run, isInsert: false, ct);

                var stat = await ProcessSingleSourceAsync(run, source, ct);
                run.SourceStats.Add(stat);

                run.TotalRetrieved += stat.TotalRetrieved;
                run.TotalNew += stat.NewCount;
                run.TotalUpdated += stat.UpdatedCount;
                run.TotalSkipped += stat.SkippedCount;
                run.TotalRemoved += stat.RemovedCount;
                run.TotalFailed += stat.FailedCount;

                if (stat.Status is "SUCCESS" or "COMPLETED_WITH_WARNINGS")
                {
                    run.SourcesSucceeded++;
                }
                else
                {
                    run.SourcesFailed++;
                }

                await PersistRunHeaderAsync(run, isInsert: false, ct);
            }

            run.CurrentSource = null;
            runSw.Stop();
            run.DurationMs = runSw.ElapsedMilliseconds;
            run.CompletedAt = DateTimeOffset.UtcNow;

            if (run.SourcesProcessed == 0)
            {
                run.Status = "COMPLETED_WITH_WARNINGS";
            }
            else if (run.SourcesFailed == 0 && run.TotalFailed == 0 && run.Errors.Count == 0)
            {
                run.Status = "SUCCESS";
            }
            else if (run.SourcesFailed == 0)
            {
                run.Status = "COMPLETED_WITH_WARNINGS";
            }
            else if (run.SourcesSucceeded > 0)
            {
                run.Status = "PARTIALLY_COMPLETED";
            }
            else
            {
                run.Status = "FAILED";
            }

            run.UserSummaryMessage = BlacklistErrorTranslator.BuildRunSummaryMessage(run);
            run.WormHash = ComputeRunWormHash(run);

            await PersistRunHeaderAsync(run, isInsert: false, ct);
            await PurgeExpiredSkippedItemsAsync(ct);
            await RecordScheduledRunDateIfApplicableAsync(run, ct);
        }
        catch (Exception ex)
        {
            runSw.Stop();
            run.DurationMs = runSw.ElapsedMilliseconds;
            run.CompletedAt = DateTimeOffset.UtcNow;
            run.CurrentSource = null;
            run.Status = run.SourcesSucceeded > 0 ? "PARTIALLY_COMPLETED" : "FAILED";
            var translated = BlacklistErrorTranslator.TranslateException("SYSTEM", ex, "EXECUTION");
            run.UserSummaryMessage = translated.UserMessage;
            await RecordErrorAsync(run, "SYSTEM", translated, null, false, ct);
            await PersistRunHeaderAsync(run, isInsert: false, ct);
        }
        finally
        {
            if (lockConn is not null)
            {
                try
                {
                    await using var unlockCmd = new NpgsqlCommand("SELECT pg_advisory_unlock(@key);", lockConn);
                    unlockCmd.Parameters.AddWithValue("key", AdvisoryLockKey);
                    await unlockCmd.ExecuteNonQueryAsync(CancellationToken.None);
                }
                catch
                {
                    // Connection disposal also releases session advisory lock
                }
                await lockConn.DisposeAsync();
            }
        }
    }

    private async Task<BlacklistSourceStat> ProcessSingleSourceAsync(
        BlacklistSyncRun run,
        BlacklistSourceConfig source,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var stat = new BlacklistSourceStat
        {
            RunId = run.Id,
            SourceType = source.SourceType,
            SourceRefId = source.SourceRefId,
            SourceUrl = source.Url,
            StartedAt = DateTimeOffset.UtcNow,
            Status = "RUNNING"
        };

        SanctionsFeedParseOutcome? parseOutcome = null;
        Exception? lastFetchEx = null;
        int attempts = 0;

        for (attempts = 1; attempts <= 3; attempts++)
        {
            stat.AttemptCount = attempts;
            try
            {
                if (Uri.TryCreate(source.Url, UriKind.Absolute, out var sourceUri) && sourceUri.Scheme == Uri.UriSchemeFile)
                {
                    if (!File.Exists(sourceUri.LocalPath))
                    {
                        throw new FileNotFoundException($"Feed file not found at '{sourceUri.LocalPath}'.");
                    }
                    await using var fileStream = File.OpenRead(sourceUri.LocalPath);
                    parseOutcome = await SanctionsFeedParsers.ParseXmlStreamAsync(source.SourceType, source.SourceRefId, fileStream, maxRecords: null, ct: ct);
                    lastFetchEx = null;
                    break;
                }

                using var req = new HttpRequestMessage(HttpMethod.Get, source.Url);
                using var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"HTTP {(int)resp.StatusCode} ({resp.StatusCode})", null, resp.StatusCode);
                }

                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                parseOutcome = await SanctionsFeedParsers.ParseXmlStreamAsync(source.SourceType, source.SourceRefId, stream, maxRecords: null, ct: ct);
                lastFetchEx = null;
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastFetchEx = ex;
                var translated = BlacklistErrorTranslator.TranslateException(source.SourceType, ex, "FETCH");
                if (!translated.IsRecoverable || attempts >= 3)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(400 * attempts), ct);
            }
        }

        if (parseOutcome is null || !parseOutcome.IsRootCompleted || lastFetchEx is not null)
        {
            sw.Stop();
            stat.DurationMs = sw.ElapsedMilliseconds;
            stat.CompletedAt = DateTimeOffset.UtcNow;
            stat.Status = "FAILED";
            stat.FailedCount = 1;

            var translated = BlacklistErrorTranslator.TranslateException(
                source.SourceType,
                lastFetchEx ?? new XmlException($"Incomplete XML stream for {source.SourceType}"),
                "FETCH");

            stat.UserErrorMessage = translated.UserMessage;
            stat.TechnicalErrorDetails = translated.TechnicalDetails;

            await RecordErrorAsync(run, source.SourceType, translated, null, attempts > 1, ct);
            await RecordFailedItemAsync(run, source, "SOURCE_FEED", $"{source.SourceType} Feed Retrieval", translated.UserMessage, translated.ErrorCategory, ct);
            await UpdateSourceStatusAfterRunAsync(source, stat, ct);
            await PersistSourceStatAsync(stat, ct);
            return stat;
        }

        // Log any individual malformed XML entries while continuing to process valid entries
        foreach (var (badId, badMsg) in parseOutcome.MalformedRecords)
        {
            stat.FailedCount++;
            var err = new TranslatedBlacklistError(
                "MALFORMED_RECORD",
                $"A record ({badId}) in the {source.SourceType} feed had invalid or incomplete XML fields and was skipped for review.",
                badMsg,
                false);
            await RecordErrorAsync(run, source.SourceType, err, badId, false, ct);
            await RecordFailedItemAsync(run, source, badId, $"Malformed {source.SourceType} Record", err.UserMessage, "MALFORMED_RECORD", ct);
        }

        // Deduplicate incoming records within the same source feed by EntityExternalId (keep latest in feed)
        var incomingByExternalId = new Dictionary<string, NormalizedBlacklistRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var rec in parseOutcome.Records)
        {
            incomingByExternalId[rec.EntityExternalId] = rec;
        }

        stat.TotalRetrieved = incomingByExternalId.Count;

        // Safeguard: If a feed returned 0 valid records, treat as suspicious/empty feed and do not wipe active records
        if (incomingByExternalId.Count == 0)
        {
            sw.Stop();
            stat.DurationMs = sw.ElapsedMilliseconds;
            stat.CompletedAt = DateTimeOffset.UtcNow;
            stat.Status = "FAILED";
            stat.FailedCount++;
            var emptyErr = new TranslatedBlacklistError(
                "INVALID_XML_FORMAT",
                $"The {source.SourceType} file was downloaded, but its XML format is invalid or incomplete. Existing {source.SourceType} records were not modified.",
                $"Parsed 0 valid records from root <{parseOutcome.RootElementName}>.",
                false);
            stat.UserErrorMessage = emptyErr.UserMessage;
            stat.TechnicalErrorDetails = emptyErr.TechnicalDetails;
            await RecordErrorAsync(run, source.SourceType, emptyErr, null, false, ct);
            await UpdateSourceStatusAfterRunAsync(source, stat, ct);
            await PersistSourceStatAsync(stat, ct);
            return stat;
        }

        // Load existing entities for this source
        Dictionary<string, StoredWatchlistSnapshot> existingMap;
        try
        {
            existingMap = await LoadExistingEntitiesForSourceAsync(source.SourceType, ct);
        }
        catch (Exception ex)
        {
            sw.Stop();
            stat.DurationMs = sw.ElapsedMilliseconds;
            stat.CompletedAt = DateTimeOffset.UtcNow;
            stat.Status = "FAILED";
            stat.FailedCount++;
            var dupErr = BlacklistErrorTranslator.TranslateException(source.SourceType, ex, "DUPLICATE_LOOKUP");
            stat.UserErrorMessage = dupErr.UserMessage;
            stat.TechnicalErrorDetails = dupErr.TechnicalDetails;
            await RecordErrorAsync(run, source.SourceType, dupErr, null, false, ct);
            await UpdateSourceStatusAfterRunAsync(source, stat, ct);
            await PersistSourceStatAsync(stat, ct);
            return stat;
        }

        var entitiesToUpsert = new List<(Guid Id, NormalizedBlacklistRecord Record, string ContentHash, string DetailsJson, bool IsNew)>();
        var itemsToLog = new List<BlacklistSyncItem>(incomingByExternalId.Count + 64);
        var seenExternalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;
        var skippedLoggedCount = 0;

        foreach (var (extId, incoming) in incomingByExternalId)
        {
            seenExternalIds.Add(extId);
            var contentHash = BlacklistRecordComparer.ComputeContentHash(incoming);
            var detailsJson = BlacklistRecordComparer.SerializeDetailsJson(incoming);

            if (!existingMap.TryGetValue(extId, out var existing))
            {
                var entityId = Guid.NewGuid();
                entitiesToUpsert.Add((entityId, incoming, contentHash, detailsJson, true));
                stat.NewCount++;
                itemsToLog.Add(new BlacklistSyncItem
                {
                    RunId = run.Id,
                    SourceType = source.SourceType,
                    SourceRefId = source.SourceRefId,
                    EntityExternalId = extId,
                    FullName = incoming.FullName,
                    Action = "NEW",
                    Reason = $"New sanction designation added from {source.SourceType} feed.",
                    ComparisonResult = "NEW_RECORD",
                    PreviousDetailsJson = null,
                    CurrentDetailsJson = detailsJson,
                    ProcessedAt = now
                });
            }
            else
            {
                var diff = BlacklistRecordComparer.Compare(
                    existing.Record,
                    existing.ContentHash,
                    existing.IsActive,
                    incoming);

                if (diff.IsExactMatch)
                {
                    stat.SkippedCount++;
                    // shortcut: Retain first 100 SKIPPED entries per run in audit table to avoid inserting millions of unchanged rows into Postgres WAL, upgrade to partitioned archival if full skipped log is required
                    if (skippedLoggedCount < 100)
                    {
                        skippedLoggedCount++;
                        itemsToLog.Add(new BlacklistSyncItem
                        {
                            RunId = run.Id,
                            SourceType = source.SourceType,
                            SourceRefId = source.SourceRefId,
                            EntityExternalId = extId,
                            FullName = incoming.FullName,
                            Action = "SKIPPED",
                            Reason = diff.Reason,
                            ComparisonResult = diff.ComparisonResult,
                            PreviousDetailsJson = null,
                            CurrentDetailsJson = null,
                            ProcessedAt = now
                        });
                    }
                }
                else
                {
                    entitiesToUpsert.Add((existing.Id, incoming, contentHash, detailsJson, false));
                    stat.UpdatedCount++;
                    itemsToLog.Add(new BlacklistSyncItem
                    {
                        RunId = run.Id,
                        SourceType = source.SourceType,
                        SourceRefId = source.SourceRefId,
                        EntityExternalId = extId,
                        FullName = incoming.FullName,
                        Action = "UPDATED",
                        Reason = diff.Reason,
                        ComparisonResult = diff.ComparisonResult,
                        PreviousDetailsJson = diff.PreviousDetailsJson ?? existing.AttributesJson,
                        CurrentDetailsJson = diff.CurrentDetailsJson ?? detailsJson,
                        ProcessedAt = now
                    });
                }
            }
        }

        // Detect REMOVED records (only because parseOutcome.IsRootCompleted == true and incomingByExternalId.Count > 0)
        var removedEntities = new List<(Guid Id, string ExternalId, string FullName, string Reason)>();
        foreach (var (extId, existing) in existingMap)
        {
            if (existing.IsActive && !seenExternalIds.Contains(extId))
            {
                var reason = $"Record no longer present in the latest validated {source.SourceType} dataset; marked inactive (REMOVED).";
                removedEntities.Add((existing.Id, extId, existing.Record.FullName, reason));
                stat.RemovedCount++;
                itemsToLog.Add(new BlacklistSyncItem
                {
                    RunId = run.Id,
                    SourceType = source.SourceType,
                    SourceRefId = source.SourceRefId,
                    EntityExternalId = extId,
                    FullName = existing.Record.FullName,
                    Action = "REMOVED",
                    Reason = reason,
                    ComparisonResult = "MISSING_FROM_LATEST_FEED",
                    PreviousDetailsJson = existing.AttributesJson ?? BlacklistRecordComparer.SerializeDetailsJson(existing.Record),
                    CurrentDetailsJson = null,
                    ProcessedAt = now
                });
            }
        }

        // Persist changes in batches
        try
        {
            await PersistSourceChangesAsync(
                run.Id,
                source,
                entitiesToUpsert,
                removedEntities,
                itemsToLog,
                seenExternalIds,
                ct);
        }
        catch (Exception ex)
        {
            sw.Stop();
            stat.DurationMs = sw.ElapsedMilliseconds;
            stat.CompletedAt = DateTimeOffset.UtcNow;
            stat.Status = "FAILED";
            stat.FailedCount++;
            var writeErr = BlacklistErrorTranslator.TranslateException(source.SourceType, ex, "DB_WRITE");
            stat.UserErrorMessage = writeErr.UserMessage;
            stat.TechnicalErrorDetails = writeErr.TechnicalDetails;
            await RecordErrorAsync(run, source.SourceType, writeErr, null, false, ct);
            await UpdateSourceStatusAfterRunAsync(source, stat, ct);
            await PersistSourceStatAsync(stat, ct);
            return stat;
        }

        sw.Stop();
        stat.DurationMs = sw.ElapsedMilliseconds;
        stat.CompletedAt = DateTimeOffset.UtcNow;
        stat.Status = stat.FailedCount > 0 ? "COMPLETED_WITH_WARNINGS" : "SUCCESS";

        await UpdateSourceStatusAfterRunAsync(source, stat, ct);
        await PersistSourceStatAsync(stat, ct);
        return stat;
    }

    private async Task<Dictionary<string, StoredWatchlistSnapshot>> LoadExistingEntitiesForSourceAsync(string sourceType, CancellationToken ct)
    {
        var result = new Dictionary<string, StoredWatchlistSnapshot>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in _memEntities)
        {
            if (string.Equals(kvp.Value.Record.SourceType, sourceType, StringComparison.OrdinalIgnoreCase))
            {
                result[kvp.Value.Record.EntityExternalId] = kvp.Value;
            }
        }

        if (!HasDatabase)
        {
            return result;
        }

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT id, entity_external_id, entity_type, full_name, dob, nationality,
                   source_list, category, id_details, alias_name, is_active,
                   source_ref_id, addresses, programs, remarks, attributes_json::text, content_hash
            FROM screening.watchlist_entities
            WHERE source_type = @src;
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("src", sourceType);

        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var id = r.GetGuid(0);
            var extId = r.GetString(1);
            var entityType = r.GetString(2);
            var fullName = r.GetString(3);
            var dob = r.IsDBNull(4) ? (DateOnly?)null : r.GetFieldValue<DateOnly>(4);
            var nat = r.IsDBNull(5) ? null : r.GetString(5);
            var srcList = r.GetString(6);
            var cat = r.GetString(7);
            var idDetails = r.IsDBNull(8) ? null : r.GetString(8);
            var aliasName = r.IsDBNull(9) ? null : r.GetString(9);
            var isActive = r.GetBoolean(10);
            var srcRef = r.IsDBNull(11) ? GetDefaultRefId(sourceType) : r.GetString(11);
            var addresses = r.IsDBNull(12) ? null : r.GetString(12);
            var programs = r.IsDBNull(13) ? null : r.GetString(13);
            var remarks = r.IsDBNull(14) ? null : r.GetString(14);
            var attrJson = r.IsDBNull(15) ? null : r.GetString(15);
            var contentHash = r.IsDBNull(16) ? null : r.GetString(16);

            var reconstructed = ReconstructRecord(
                sourceType, srcRef, srcList, extId, entityType, fullName, cat,
                dob, aliasName, idDetails, addresses, nat, programs, remarks, attrJson);

            var computedHash = string.IsNullOrWhiteSpace(contentHash)
                ? BlacklistRecordComparer.ComputeContentHash(reconstructed)
                : contentHash.Trim();

            var snap = new StoredWatchlistSnapshot(id, reconstructed, computedHash, attrJson, isActive);
            result[extId] = snap;
            _memEntities[$"{sourceType}:{extId}"] = snap;
        }

        return result;
    }

    private static NormalizedBlacklistRecord ReconstructRecord(
        string sourceType,
        string sourceRefId,
        string sourceList,
        string extId,
        string entityType,
        string fullName,
        string category,
        DateOnly? dob,
        string? aliasSummary,
        string? idSummary,
        string? addrSummary,
        string? natSummary,
        string? progSummary,
        string? remarks,
        string? attrJson)
    {
        if (!string.IsNullOrWhiteSpace(attrJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(attrJson);
                var root = doc.RootElement;
                return BlacklistRecordComparer.CanonicalizeRecord(new NormalizedBlacklistRecord
                {
                    SourceType = sourceType,
                    SourceRefId = sourceRefId,
                    SourceList = sourceList,
                    EntityExternalId = extId,
                    EntityType = root.TryGetProperty("entityType", out var et) ? et.GetString() ?? entityType : entityType,
                    FullName = root.TryGetProperty("fullName", out var fn) ? fn.GetString() ?? fullName : fullName,
                    Category = category,
                    PrimaryDob = dob,
                    Aliases = ReadStringArray(root, "aliases"),
                    DatesOfBirth = ReadStringArray(root, "datesOfBirth"),
                    Identifications = ReadStringArray(root, "identifications"),
                    Addresses = ReadStringArray(root, "addresses"),
                    Nationalities = ReadStringArray(root, "nationalities"),
                    Programs = ReadStringArray(root, "programs"),
                    Remarks = root.TryGetProperty("remarks", out var rem) ? rem.GetString() : remarks
                });
            }
            catch
            {
                // Fallback to column split below
            }
        }

        return BlacklistRecordComparer.CanonicalizeRecord(new NormalizedBlacklistRecord
        {
            SourceType = sourceType,
            SourceRefId = sourceRefId,
            SourceList = sourceList,
            EntityExternalId = extId,
            EntityType = entityType,
            FullName = fullName,
            Category = category,
            PrimaryDob = dob,
            Aliases = BlacklistRecordComparer.CanonicalizeList(aliasSummary is null ? [] : [aliasSummary]),
            DatesOfBirth = dob.HasValue ? [dob.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)] : [],
            Identifications = BlacklistRecordComparer.CanonicalizeList(idSummary is null ? [] : [idSummary]),
            Addresses = BlacklistRecordComparer.CanonicalizeList(addrSummary is null ? [] : [addrSummary]),
            Nationalities = BlacklistRecordComparer.CanonicalizeList(natSummary is null ? [] : [natSummary]),
            Programs = BlacklistRecordComparer.CanonicalizeList(progSummary is null ? [] : [progSummary]),
            Remarks = remarks
        });
    }

    private static List<string> ReadStringArray(JsonElement root, string propName)
    {
        if (!root.TryGetProperty(propName, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<string>();
        foreach (var item in arr.EnumerateArray())
        {
            var s = item.GetString();
            if (!string.IsNullOrWhiteSpace(s))
            {
                list.Add(s);
            }
        }
        return list;
    }

    private async Task PersistSourceChangesAsync(
        Guid runId,
        BlacklistSourceConfig source,
        List<(Guid Id, NormalizedBlacklistRecord Record, string ContentHash, string DetailsJson, bool IsNew)> entitiesToUpsert,
        List<(Guid Id, string ExternalId, string FullName, string Reason)> removedEntities,
        List<BlacklistSyncItem> itemsToLog,
        HashSet<string> seenExternalIds,
        CancellationToken ct)
    {
        // 1. Update in-memory state first
        foreach (var (id, rec, hash, json, _) in entitiesToUpsert)
        {
            _memEntities[$"{source.SourceType}:{rec.EntityExternalId}"] = new StoredWatchlistSnapshot(id, rec, hash, json, true);
        }

        foreach (var (_, extId, _, _) in removedEntities)
        {
            if (_memEntities.TryGetValue($"{source.SourceType}:{extId}", out var existing))
            {
                _memEntities[$"{source.SourceType}:{extId}"] = existing with { IsActive = false };
            }
        }

        if (_memItems.TryGetValue(runId, out var memRunItems))
        {
            lock (memRunItems)
            {
                long nextId = memRunItems.Count + 1;
                foreach (var item in itemsToLog)
                {
                    item.Id = nextId++;
                    memRunItems.Add(item);
                }
            }
        }

        // 2. Index active upserted entities in search engine
        if (entitiesToUpsert.Count > 0)
        {
            var watchlistBatch = entitiesToUpsert.Select(e => new WatchlistEntity
            {
                Id = e.Id,
                EntityExternalId = e.Record.EntityExternalId,
                EntityType = e.Record.EntityType,
                FullName = e.Record.FullName,
                Dob = e.Record.PrimaryDob,
                Nationality = Truncate(e.Record.NationalitySummary, 100),
                SourceList = e.Record.SourceList,
                Category = e.Record.Category,
                AliasName = e.Record.AliasSummary,
                IdDetails = e.Record.IdDetailsSummary,
                IsBlocked = true,
                IsActive = true
            }).ToList();

            if (_inMemorySearchEngine is not null)
            {
                await _inMemorySearchEngine.IndexBatchAsync(watchlistBatch, ct);
            }

            if (_searchEngine is not null && !ReferenceEquals(_searchEngine, _inMemorySearchEngine))
            {
                try
                {
                    foreach (var chunk in watchlistBatch.Chunk(500))
                    {
                        await _searchEngine.IndexBatchAsync(chunk, ct);
                    }
                }
                catch (Exception)
                {
                    // Search engine indexing is non-fatal when OpenSearch is offline
                }
            }
        }

        if (!HasDatabase)
        {
            return;
        }

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // 3. Batch upsert NEW & UPDATED watchlist entities
        foreach (var chunk in entitiesToUpsert.Chunk(300))
        {
            await using var batch = new NpgsqlBatch(conn);
            foreach (var (id, rec, hash, json, _) in chunk)
            {
                var cmd = new NpgsqlBatchCommand("""
                    INSERT INTO screening.watchlist_entities (
                        id, entity_external_id, entity_type, full_name, dob, nationality,
                        source_list, category, id_details, alias_name, is_blocked, is_active,
                        source_type, source_ref_id, addresses, programs, remarks,
                        attributes_json, content_hash, updated_at, last_seen_at, last_run_id,
                        removed_at, removed_in_run_id, removal_reason
                    )
                    VALUES (
                        $1, $2, $3, $4, $5, $6,
                        $7, $8, $9, $10, true, true,
                        $11, $12, $13, $14, $15,
                        $16::jsonb, $17, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, $18,
                        NULL, NULL, NULL
                    )
                    ON CONFLICT (entity_external_id, full_name) DO UPDATE SET
                        entity_type = EXCLUDED.entity_type,
                        dob = EXCLUDED.dob,
                        nationality = EXCLUDED.nationality,
                        source_list = EXCLUDED.source_list,
                        category = EXCLUDED.category,
                        id_details = EXCLUDED.id_details,
                        alias_name = EXCLUDED.alias_name,
                        is_blocked = true,
                        is_active = true,
                        source_type = EXCLUDED.source_type,
                        source_ref_id = EXCLUDED.source_ref_id,
                        addresses = EXCLUDED.addresses,
                        programs = EXCLUDED.programs,
                        remarks = EXCLUDED.remarks,
                        attributes_json = EXCLUDED.attributes_json,
                        content_hash = EXCLUDED.content_hash,
                        updated_at = CURRENT_TIMESTAMP,
                        last_seen_at = CURRENT_TIMESTAMP,
                        last_run_id = EXCLUDED.last_run_id,
                        removed_at = NULL,
                        removed_in_run_id = NULL,
                        removal_reason = NULL;
                    """);

                cmd.Parameters.Add(new NpgsqlParameter { Value = id });
                cmd.Parameters.Add(new NpgsqlParameter { Value = Truncate(rec.EntityExternalId, 100) ?? string.Empty });
                cmd.Parameters.Add(new NpgsqlParameter { Value = Truncate(rec.EntityType, 30) ?? "INDIVIDUAL" });
                cmd.Parameters.Add(new NpgsqlParameter { Value = rec.FullName });
                cmd.Parameters.Add(new NpgsqlParameter { Value = rec.PrimaryDob.HasValue ? (object)rec.PrimaryDob.Value : DBNull.Value });
                cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)Truncate(rec.NationalitySummary, 100) ?? DBNull.Value });
                cmd.Parameters.Add(new NpgsqlParameter { Value = Truncate(rec.SourceList, 50) ?? source.SourceType });
                cmd.Parameters.Add(new NpgsqlParameter { Value = Truncate(rec.Category, 50) ?? "SANCTION" });
                cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)rec.IdDetailsSummary ?? DBNull.Value });
                cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)rec.AliasSummary ?? DBNull.Value });
                cmd.Parameters.Add(new NpgsqlParameter { Value = source.SourceType });
                cmd.Parameters.Add(new NpgsqlParameter { Value = source.SourceRefId });
                cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)rec.AddressSummary ?? DBNull.Value });
                cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)rec.ProgramSummary ?? DBNull.Value });
                cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)rec.Remarks ?? DBNull.Value });
                cmd.Parameters.Add(new NpgsqlParameter { Value = json });
                cmd.Parameters.Add(new NpgsqlParameter { Value = hash });
                cmd.Parameters.Add(new NpgsqlParameter { Value = runId });

                batch.BatchCommands.Add(cmd);
            }

            await batch.ExecuteNonQueryAsync(ct);
        }

        // If any UPDATED entity changed its FullName for the same (source_type, entity_external_id), update by id directly
        foreach (var chunk in entitiesToUpsert.Where(e => !e.IsNew).Chunk(300))
        {
            await using var batch = new NpgsqlBatch(conn);
            foreach (var (id, rec, hash, json, _) in chunk)
            {
                var cmd = new NpgsqlBatchCommand("""
                    UPDATE screening.watchlist_entities
                    SET full_name = $2,
                        entity_type = $3,
                        dob = $4,
                        nationality = $5,
                        id_details = $6,
                        alias_name = $7,
                        addresses = $8,
                        programs = $9,
                        remarks = $10,
                        attributes_json = $11::jsonb,
                        content_hash = $12,
                        is_active = true,
                        updated_at = CURRENT_TIMESTAMP,
                        last_seen_at = CURRENT_TIMESTAMP,
                        last_run_id = $13,
                        removed_at = NULL,
                        removed_in_run_id = NULL,
                        removal_reason = NULL
                    WHERE id = $1;
                    """);
                cmd.Parameters.Add(new NpgsqlParameter { Value = id });
                cmd.Parameters.Add(new NpgsqlParameter { Value = rec.FullName });
                cmd.Parameters.Add(new NpgsqlParameter { Value = Truncate(rec.EntityType, 30) ?? "INDIVIDUAL" });
                cmd.Parameters.Add(new NpgsqlParameter { Value = rec.PrimaryDob.HasValue ? (object)rec.PrimaryDob.Value : DBNull.Value });
                cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)Truncate(rec.NationalitySummary, 100) ?? DBNull.Value });
                cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)rec.IdDetailsSummary ?? DBNull.Value });
                cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)rec.AliasSummary ?? DBNull.Value });
                cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)rec.AddressSummary ?? DBNull.Value });
                cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)rec.ProgramSummary ?? DBNull.Value });
                cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)rec.Remarks ?? DBNull.Value });
                cmd.Parameters.Add(new NpgsqlParameter { Value = json });
                cmd.Parameters.Add(new NpgsqlParameter { Value = hash });
                cmd.Parameters.Add(new NpgsqlParameter { Value = runId });
                batch.BatchCommands.Add(cmd);
            }
            await batch.ExecuteNonQueryAsync(ct);
        }

        // 4. Touch last_seen_at for active records of this source (skip massive bulk feeds like WORLD_CHECK to avoid GIN index thrashing and WAL exhaustion)
        // shortcut: Only touch last_seen_at for non-WORLD_CHECK sources older than 1 day, upgrade to background chunking if per-run last_seen_at is required for 2.5M records
        if (seenExternalIds.Count > 0 && !string.Equals(source.SourceType, "WORLD_CHECK", StringComparison.OrdinalIgnoreCase))
        {
            await using var seenCmd = new NpgsqlCommand("""
                UPDATE screening.watchlist_entities
                SET last_seen_at = CURRENT_TIMESTAMP,
                    last_run_id = @run_id
                WHERE source_type = @src 
                  AND is_active = true
                  AND (last_seen_at IS NULL OR last_seen_at < CURRENT_TIMESTAMP - INTERVAL '1 day');
                """, conn);
            seenCmd.Parameters.AddWithValue("run_id", runId);
            seenCmd.Parameters.AddWithValue("src", source.SourceType);
            await seenCmd.ExecuteNonQueryAsync(ct);
        }

        // 5. Mark REMOVED entities inactive (never hard-delete)
        foreach (var chunk in removedEntities.Chunk(300))
        {
            await using var batch = new NpgsqlBatch(conn);
            foreach (var (id, _, _, reason) in chunk)
            {
                var cmd = new NpgsqlBatchCommand("""
                    UPDATE screening.watchlist_entities
                    SET is_active = false,
                        removed_at = CURRENT_TIMESTAMP,
                        removed_in_run_id = $2,
                        removal_reason = $3,
                        updated_at = CURRENT_TIMESTAMP
                    WHERE id = $1;
                    """);
                cmd.Parameters.Add(new NpgsqlParameter { Value = id });
                cmd.Parameters.Add(new NpgsqlParameter { Value = runId });
                cmd.Parameters.Add(new NpgsqlParameter { Value = reason });
                batch.BatchCommands.Add(cmd);
            }
            await batch.ExecuteNonQueryAsync(ct);
        }

        // 6. Batch insert all audit items (NEW, UPDATED, SKIPPED, REMOVED) into screening.blacklist_sync_items
        foreach (var chunk in itemsToLog.Chunk(500))
        {
            await using var batch = new NpgsqlBatch(conn);
            foreach (var item in chunk)
            {
                var cmd = new NpgsqlBatchCommand("""
                    INSERT INTO screening.blacklist_sync_items (
                        run_id, source_type, source_ref_id, entity_external_id, full_name,
                        action, reason, comparison_result, previous_details, current_details, processed_at
                    )
                    VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9::jsonb, $10::jsonb, $11);
                    """);

                cmd.Parameters.Add(new NpgsqlParameter { Value = item.RunId });
                cmd.Parameters.Add(new NpgsqlParameter { Value = item.SourceType });
                cmd.Parameters.Add(new NpgsqlParameter { Value = item.SourceRefId });
                cmd.Parameters.Add(new NpgsqlParameter { Value = Truncate(item.EntityExternalId, 255) ?? string.Empty });
                cmd.Parameters.Add(new NpgsqlParameter { Value = item.FullName });
                cmd.Parameters.Add(new NpgsqlParameter { Value = item.Action });
                cmd.Parameters.Add(new NpgsqlParameter { Value = item.Reason });
                cmd.Parameters.Add(new NpgsqlParameter { Value = Truncate(item.ComparisonResult, 64) ?? "MATCH" });
                cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)item.PreviousDetailsJson ?? DBNull.Value });
                cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)item.CurrentDetailsJson ?? DBNull.Value });
                cmd.Parameters.Add(new NpgsqlParameter { Value = item.ProcessedAt });

                batch.BatchCommands.Add(cmd);
            }
            await batch.ExecuteNonQueryAsync(ct);
        }
    }

    private async Task RecordErrorAsync(
        BlacklistSyncRun run,
        string sourceType,
        TranslatedBlacklistError translated,
        string? affectedRecordId,
        bool retryAttempted,
        CancellationToken ct)
    {
        var err = new BlacklistSyncError
        {
            Id = run.Errors.Count + 1,
            RunId = run.Id,
            SourceType = sourceType,
            ErrorCategory = translated.ErrorCategory,
            UserMessage = translated.UserMessage,
            TechnicalDetails = translated.TechnicalDetails,
            AffectedRecordId = affectedRecordId,
            IsRecoverable = translated.IsRecoverable,
            RetryAttempted = retryAttempted,
            OccurredAt = DateTimeOffset.UtcNow
        };
        run.Errors.Add(err);

        if (!HasDatabase)
        {
            return;
        }

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            const string sql = """
                INSERT INTO screening.blacklist_sync_errors (
                    run_id, source_type, error_category, user_message, technical_details,
                    affected_record_id, is_recoverable, retry_attempted, occurred_at
                )
                VALUES (@run_id, @src, @cat, @user_msg, @tech, @rec_id, @recov, @retry, @at);
                """;

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("run_id", err.RunId);
            cmd.Parameters.AddWithValue("src", err.SourceType);
            cmd.Parameters.AddWithValue("cat", err.ErrorCategory);
            cmd.Parameters.AddWithValue("user_msg", err.UserMessage);
            cmd.Parameters.AddWithValue("tech", err.TechnicalDetails);
            cmd.Parameters.AddWithValue("rec_id", (object?)err.AffectedRecordId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("recov", err.IsRecoverable);
            cmd.Parameters.AddWithValue("retry", err.RetryAttempted);
            cmd.Parameters.AddWithValue("at", err.OccurredAt);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch
        {
            // Stored in memory on run
        }
    }

    private async Task RecordFailedItemAsync(
        BlacklistSyncRun run,
        BlacklistSourceConfig source,
        string recordId,
        string fullName,
        string reason,
        string comparisonResult,
        CancellationToken ct)
    {
        var item = new BlacklistSyncItem
        {
            RunId = run.Id,
            SourceType = source.SourceType,
            SourceRefId = source.SourceRefId,
            EntityExternalId = recordId,
            FullName = fullName,
            Action = "FAILED",
            Reason = reason,
            ComparisonResult = comparisonResult,
            ProcessedAt = DateTimeOffset.UtcNow
        };

        if (_memItems.TryGetValue(run.Id, out var list))
        {
            lock (list)
            {
                item.Id = list.Count + 1;
                list.Add(item);
            }
        }

        if (!HasDatabase)
        {
            return;
        }

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            const string sql = """
                INSERT INTO screening.blacklist_sync_items (
                    run_id, source_type, source_ref_id, entity_external_id, full_name,
                    action, reason, comparison_result, processed_at
                )
                VALUES (@run_id, @src, @ref, @ext_id, @name, 'FAILED', @reason, @comp, @at);
                """;

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("run_id", item.RunId);
            cmd.Parameters.AddWithValue("src", item.SourceType);
            cmd.Parameters.AddWithValue("ref", item.SourceRefId);
            cmd.Parameters.AddWithValue("ext_id", Truncate(item.EntityExternalId, 255) ?? "FAILED");
            cmd.Parameters.AddWithValue("name", item.FullName);
            cmd.Parameters.AddWithValue("reason", item.Reason);
            cmd.Parameters.AddWithValue("comp", Truncate(item.ComparisonResult, 64) ?? "FAILED");
            cmd.Parameters.AddWithValue("at", item.ProcessedAt);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch
        {
            // Non-fatal
        }
    }

    private async Task UpdateSourceStatusAfterRunAsync(BlacklistSourceConfig source, BlacklistSourceStat stat, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        source.LastAttemptAt = now;
        source.LastStatus = stat.Status;
        source.LastErrorUserMessage = stat.UserErrorMessage;

        if (stat.Status is "SUCCESS" or "COMPLETED_WITH_WARNINGS")
        {
            source.LastSuccessfulSyncAt = now;
            source.ActiveRecordCount = stat.NewCount + stat.UpdatedCount + stat.SkippedCount;
        }

        _memSources[source.SourceType] = source;

        if (!HasDatabase)
        {
            return;
        }

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            const string sql = """
                UPDATE screening.blacklist_sources
                SET last_attempt_at = @attempt,
                    last_successful_sync_at = CASE WHEN @is_ok THEN @attempt ELSE last_successful_sync_at END,
                    last_status = @status,
                    last_error_user_message = @err,
                    active_record_count = CASE WHEN @is_ok THEN @active_cnt ELSE active_record_count END,
                    updated_at = CURRENT_TIMESTAMP
                WHERE source_type = @src;
                """;

            var isOk = stat.Status is "SUCCESS" or "COMPLETED_WITH_WARNINGS";
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("attempt", now);
            cmd.Parameters.AddWithValue("is_ok", isOk);
            cmd.Parameters.AddWithValue("status", stat.Status);
            cmd.Parameters.AddWithValue("err", (object?)stat.UserErrorMessage ?? DBNull.Value);
            cmd.Parameters.AddWithValue("active_cnt", source.ActiveRecordCount);
            cmd.Parameters.AddWithValue("src", source.SourceType);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch
        {
            // Non-fatal
        }
    }

    private async Task PersistRunHeaderAsync(BlacklistSyncRun run, bool isInsert, CancellationToken ct)
    {
        _memRuns[run.Id] = run;
        if (!HasDatabase)
        {
            return;
        }

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            if (isInsert)
            {
                const string insertSql = """
                    INSERT INTO screening.blacklist_sync_runs (
                        id, trigger_mode, triggered_by, started_at, status, current_source
                    )
                    VALUES (@id, @mode, @by, @started, @status, @curr);
                    """;
                await using var cmd = new NpgsqlCommand(insertSql, conn);
                cmd.Parameters.AddWithValue("id", run.Id);
                cmd.Parameters.AddWithValue("mode", run.TriggerMode);
                cmd.Parameters.AddWithValue("by", run.TriggeredBy);
                cmd.Parameters.AddWithValue("started", run.StartedAt);
                cmd.Parameters.AddWithValue("status", run.Status);
                cmd.Parameters.AddWithValue("curr", (object?)run.CurrentSource ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            else
            {
                const string updateSql = """
                    UPDATE screening.blacklist_sync_runs
                    SET completed_at = @completed,
                        duration_ms = @dur,
                        status = @status,
                        current_source = @curr,
                        sources_processed = @sp,
                        sources_succeeded = @ss,
                        sources_failed = @sf,
                        total_retrieved = @tr,
                        total_new = @tn,
                        total_updated = @tu,
                        total_skipped = @ts,
                        total_removed = @trem,
                        total_failed = @tfail,
                        user_summary_message = @msg,
                        worm_hash = @worm
                    WHERE id = @id;
                    """;
                await using var cmd = new NpgsqlCommand(updateSql, conn);
                cmd.Parameters.AddWithValue("id", run.Id);
                cmd.Parameters.AddWithValue("completed", (object?)run.CompletedAt ?? DBNull.Value);
                cmd.Parameters.AddWithValue("dur", run.DurationMs);
                cmd.Parameters.AddWithValue("status", run.Status);
                cmd.Parameters.AddWithValue("curr", (object?)run.CurrentSource ?? DBNull.Value);
                cmd.Parameters.AddWithValue("sp", run.SourcesProcessed);
                cmd.Parameters.AddWithValue("ss", run.SourcesSucceeded);
                cmd.Parameters.AddWithValue("sf", run.SourcesFailed);
                cmd.Parameters.AddWithValue("tr", run.TotalRetrieved);
                cmd.Parameters.AddWithValue("tn", run.TotalNew);
                cmd.Parameters.AddWithValue("tu", run.TotalUpdated);
                cmd.Parameters.AddWithValue("ts", run.TotalSkipped);
                cmd.Parameters.AddWithValue("trem", run.TotalRemoved);
                cmd.Parameters.AddWithValue("tfail", run.TotalFailed);
                cmd.Parameters.AddWithValue("msg", (object?)run.UserSummaryMessage ?? DBNull.Value);
                cmd.Parameters.AddWithValue("worm", (object?)run.WormHash ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync(ct);
            }
        }
        catch
        {
            // Non-fatal
        }
    }

    private async Task PersistSourceStatAsync(BlacklistSourceStat stat, CancellationToken ct)
    {
        if (!HasDatabase)
        {
            return;
        }

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            const string sql = """
                INSERT INTO screening.blacklist_sync_source_stats (
                    id, run_id, source_type, source_ref_id, source_url,
                    started_at, completed_at, duration_ms, attempt_count, status,
                    total_retrieved, new_count, updated_count, skipped_count, removed_count, failed_count,
                    user_error_message, technical_error_details
                )
                VALUES (
                    @id, @run_id, @src, @ref, @url,
                    @started, @completed, @dur, @attempts, @status,
                    @tr, @nc, @uc, @sc, @rc, @fc,
                    @uerr, @terr
                );
                """;

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", stat.Id);
            cmd.Parameters.AddWithValue("run_id", stat.RunId);
            cmd.Parameters.AddWithValue("src", stat.SourceType);
            cmd.Parameters.AddWithValue("ref", stat.SourceRefId);
            cmd.Parameters.AddWithValue("url", stat.SourceUrl);
            cmd.Parameters.AddWithValue("started", stat.StartedAt);
            cmd.Parameters.AddWithValue("completed", (object?)stat.CompletedAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("dur", stat.DurationMs);
            cmd.Parameters.AddWithValue("attempts", stat.AttemptCount);
            cmd.Parameters.AddWithValue("status", stat.Status);
            cmd.Parameters.AddWithValue("tr", stat.TotalRetrieved);
            cmd.Parameters.AddWithValue("nc", stat.NewCount);
            cmd.Parameters.AddWithValue("uc", stat.UpdatedCount);
            cmd.Parameters.AddWithValue("sc", stat.SkippedCount);
            cmd.Parameters.AddWithValue("rc", stat.RemovedCount);
            cmd.Parameters.AddWithValue("fc", stat.FailedCount);
            cmd.Parameters.AddWithValue("uerr", (object?)stat.UserErrorMessage ?? DBNull.Value);
            cmd.Parameters.AddWithValue("terr", (object?)stat.TechnicalErrorDetails ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch
        {
            // Non-fatal
        }
    }

    private async Task PurgeExpiredSkippedItemsAsync(CancellationToken ct)
    {
        var config = await GetScheduleConfigAsync(ct);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Max(1, config.RetentionDays));

        foreach (var list in _memItems.Values)
        {
            lock (list)
            {
                list.RemoveAll(i => i.Action == "SKIPPED" && i.ProcessedAt < cutoff);
            }
        }

        if (!HasDatabase)
        {
            return;
        }

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            const string purgeSql = """
                DELETE FROM screening.blacklist_sync_items
                WHERE id IN (
                    SELECT id FROM screening.blacklist_sync_items
                    WHERE action = 'SKIPPED' AND processed_at < @cutoff
                    LIMIT 10000
                );
                """;
            await using var cmd = new NpgsqlCommand(purgeSql, conn);
            cmd.Parameters.AddWithValue("cutoff", cutoff);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch
        {
            // Non-fatal
        }
    }

    private async Task RecordScheduledRunDateIfApplicableAsync(BlacklistSyncRun run, CancellationToken ct)
    {
        if (!string.Equals(run.TriggerMode, "SCHEDULED", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _memSchedule.LastScheduledRunDate = today;

        if (!HasDatabase)
        {
            return;
        }

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand("UPDATE screening.blacklist_sync_config SET last_scheduled_run_date = @dt WHERE id = 1;", conn);
            cmd.Parameters.AddWithValue("dt", today);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch
        {
            // Non-fatal
        }
    }

    public async Task<IReadOnlyList<BlacklistSyncRun>> GetRunsAsync(int maxResults = 30, CancellationToken ct = default)
    {
        if (HasDatabase)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(ct);

                const string sql = """
                    SELECT id, trigger_mode, triggered_by, started_at, completed_at, duration_ms,
                           status, current_source, sources_processed, sources_succeeded, sources_failed,
                           total_retrieved, total_new, total_updated, total_skipped, total_removed, total_failed,
                           user_summary_message, worm_hash
                    FROM screening.blacklist_sync_runs
                    ORDER BY started_at DESC
                    LIMIT @limit;
                    """;

                var runs = new List<BlacklistSyncRun>();
                await using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("limit", maxResults);
                    await using var r = await cmd.ExecuteReaderAsync(ct);
                    while (await r.ReadAsync(ct))
                    {
                        runs.Add(ReadRunRow(r));
                    }
                }

                if (runs.Count > 0)
                {
                    var runIds = runs.Select(r => r.Id).ToArray();
                    await PopulateRunSourceStatsAndErrorsAsync(conn, runs, runIds, ct);
                    return runs;
                }
            }
            catch
            {
                // Fallback to memory
            }
        }

        return _memRuns.Values
            .OrderByDescending(r => r.StartedAt)
            .Take(maxResults)
            .ToList();
    }

    public async Task<BlacklistSyncRun?> GetRunByIdAsync(Guid runId, CancellationToken ct = default)
    {
        if (HasDatabase)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(ct);

                const string sql = """
                    SELECT id, trigger_mode, triggered_by, started_at, completed_at, duration_ms,
                           status, current_source, sources_processed, sources_succeeded, sources_failed,
                           total_retrieved, total_new, total_updated, total_skipped, total_removed, total_failed,
                           user_summary_message, worm_hash
                    FROM screening.blacklist_sync_runs
                    WHERE id = @id;
                    """;

                BlacklistSyncRun? run = null;
                await using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("id", runId);
                    await using var r = await cmd.ExecuteReaderAsync(ct);
                    if (await r.ReadAsync(ct))
                    {
                        run = ReadRunRow(r);
                    }
                }

                if (run is not null)
                {
                    await PopulateRunSourceStatsAndErrorsAsync(conn, [run], [run.Id], ct);
                    return run;
                }
            }
            catch
            {
                // Fallback
            }
        }

        _memRuns.TryGetValue(runId, out var memRun);
        return memRun;
    }

    private static BlacklistSyncRun ReadRunRow(NpgsqlDataReader r)
    {
        return new BlacklistSyncRun
        {
            Id = r.GetGuid(0),
            TriggerMode = r.GetString(1),
            TriggeredBy = r.GetString(2),
            StartedAt = r.GetFieldValue<DateTimeOffset>(3),
            CompletedAt = r.IsDBNull(4) ? null : r.GetFieldValue<DateTimeOffset>(4),
            DurationMs = r.GetInt64(5),
            Status = r.GetString(6),
            CurrentSource = r.IsDBNull(7) ? null : r.GetString(7),
            SourcesProcessed = r.GetInt32(8),
            SourcesSucceeded = r.GetInt32(9),
            SourcesFailed = r.GetInt32(10),
            TotalRetrieved = r.GetInt32(11),
            TotalNew = r.GetInt32(12),
            TotalUpdated = r.GetInt32(13),
            TotalSkipped = r.GetInt32(14),
            TotalRemoved = r.GetInt32(15),
            TotalFailed = r.GetInt32(16),
            UserSummaryMessage = r.IsDBNull(17) ? null : r.GetString(17),
            WormHash = r.IsDBNull(18) ? null : r.GetString(18)?.Trim()
        };
    }

    private static async Task PopulateRunSourceStatsAndErrorsAsync(
        NpgsqlConnection conn,
        List<BlacklistSyncRun> runs,
        Guid[] runIds,
        CancellationToken ct)
    {
        var byId = runs.ToDictionary(r => r.Id);

        const string statsSql = """
            SELECT id, run_id, source_type, source_ref_id, source_url,
                   started_at, completed_at, duration_ms, attempt_count, status,
                   total_retrieved, new_count, updated_count, skipped_count, removed_count, failed_count,
                   user_error_message, technical_error_details
            FROM screening.blacklist_sync_source_stats
            WHERE run_id = ANY(@ids)
            ORDER BY started_at ASC;
            """;

        await using (var cmd = new NpgsqlCommand(statsSql, conn))
        {
            cmd.Parameters.AddWithValue("ids", runIds);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var stat = new BlacklistSourceStat
                {
                    Id = r.GetGuid(0),
                    RunId = r.GetGuid(1),
                    SourceType = r.GetString(2),
                    SourceRefId = r.GetString(3),
                    SourceUrl = r.GetString(4),
                    StartedAt = r.GetFieldValue<DateTimeOffset>(5),
                    CompletedAt = r.IsDBNull(6) ? null : r.GetFieldValue<DateTimeOffset>(6),
                    DurationMs = r.GetInt64(7),
                    AttemptCount = r.GetInt32(8),
                    Status = r.GetString(9),
                    TotalRetrieved = r.GetInt32(10),
                    NewCount = r.GetInt32(11),
                    UpdatedCount = r.GetInt32(12),
                    SkippedCount = r.GetInt32(13),
                    RemovedCount = r.GetInt32(14),
                    FailedCount = r.GetInt32(15),
                    UserErrorMessage = r.IsDBNull(16) ? null : r.GetString(16),
                    TechnicalErrorDetails = r.IsDBNull(17) ? null : r.GetString(17)
                };
                if (byId.TryGetValue(stat.RunId, out var parent))
                {
                    parent.SourceStats.Add(stat);
                }
            }
        }

        const string errSql = """
            SELECT id, run_id, source_type, error_category, user_message, technical_details,
                   affected_record_id, is_recoverable, retry_attempted, occurred_at
            FROM screening.blacklist_sync_errors
            WHERE run_id = ANY(@ids)
            ORDER BY occurred_at DESC;
            """;

        await using (var cmd = new NpgsqlCommand(errSql, conn))
        {
            cmd.Parameters.AddWithValue("ids", runIds);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var err = new BlacklistSyncError
                {
                    Id = r.GetInt64(0),
                    RunId = r.GetGuid(1),
                    SourceType = r.GetString(2),
                    ErrorCategory = r.GetString(3),
                    UserMessage = r.GetString(4),
                    TechnicalDetails = r.GetString(5),
                    AffectedRecordId = r.IsDBNull(6) ? null : r.GetString(6),
                    IsRecoverable = r.GetBoolean(7),
                    RetryAttempted = r.GetBoolean(8),
                    OccurredAt = r.GetFieldValue<DateTimeOffset>(9)
                };
                if (byId.TryGetValue(err.RunId, out var parent))
                {
                    parent.Errors.Add(err);
                }
            }
        }
    }

    public async Task<PaginatedSyncItemsResult> GetRunItemsAsync(
        Guid runId,
        string? action = null,
        string? sourceType = null,
        string? search = null,
        string? reasonFilter = null,
        string sortBy = "id",
        string sortDir = "asc",
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 5000);
        var act = string.IsNullOrWhiteSpace(action) || string.Equals(action, "ALL", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : action.Trim().ToUpperInvariant();
        var src = string.IsNullOrWhiteSpace(sourceType) || string.Equals(sourceType, "ALL", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : sourceType.Trim().ToUpperInvariant();
        var q = search?.Trim() ?? string.Empty;
        var rFilter = reasonFilter?.Trim() ?? string.Empty;

        if (HasDatabase)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(ct);

                const string countSql = """
                    SELECT COUNT(*)::int
                    FROM screening.blacklist_sync_items
                    WHERE run_id = @run_id
                      AND (@act = '' OR action = @act)
                      AND (@src = '' OR source_type = @src)
                      AND (@q = '' OR UPPER(full_name) LIKE '%' || UPPER(@q) || '%' OR UPPER(entity_external_id) LIKE '%' || UPPER(@q) || '%')
                      AND (@rf = '' OR UPPER(reason) LIKE '%' || UPPER(@rf) || '%' OR UPPER(comparison_result) LIKE '%' || UPPER(@rf) || '%');
                    """;

                int totalCount;
                await using (var cntCmd = new NpgsqlCommand(countSql, conn))
                {
                    cntCmd.Parameters.AddWithValue("run_id", runId);
                    cntCmd.Parameters.AddWithValue("act", act);
                    cntCmd.Parameters.AddWithValue("src", src);
                    cntCmd.Parameters.AddWithValue("q", q);
                    cntCmd.Parameters.AddWithValue("rf", rFilter);
                    totalCount = (int)(await cntCmd.ExecuteScalarAsync(ct) ?? 0);
                }

                var orderCol = sortBy?.ToLowerInvariant() switch
                {
                    "fullname" or "name" => "full_name",
                    "source" or "sourcetype" => "source_type",
                    "action" => "action",
                    "externalid" => "entity_external_id",
                    "processedat" => "processed_at",
                    _ => "id"
                };
                var dir = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";

                var dataSql = $"""
                    SELECT id, run_id, source_type, source_ref_id, entity_external_id, full_name,
                           action, reason, comparison_result, previous_details::text, current_details::text, processed_at
                    FROM screening.blacklist_sync_items
                    WHERE run_id = @run_id
                      AND (@act = '' OR action = @act)
                      AND (@src = '' OR source_type = @src)
                      AND (@q = '' OR UPPER(full_name) LIKE '%' || UPPER(@q) || '%' OR UPPER(entity_external_id) LIKE '%' || UPPER(@q) || '%')
                      AND (@rf = '' OR UPPER(reason) LIKE '%' || UPPER(@rf) || '%' OR UPPER(comparison_result) LIKE '%' || UPPER(@rf) || '%')
                    ORDER BY {orderCol} {dir}, id ASC
                    LIMIT @limit OFFSET @offset;
                    """;

                var items = new List<BlacklistSyncItem>();
                await using (var dataCmd = new NpgsqlCommand(dataSql, conn))
                {
                    dataCmd.Parameters.AddWithValue("run_id", runId);
                    dataCmd.Parameters.AddWithValue("act", act);
                    dataCmd.Parameters.AddWithValue("src", src);
                    dataCmd.Parameters.AddWithValue("q", q);
                    dataCmd.Parameters.AddWithValue("rf", rFilter);
                    dataCmd.Parameters.AddWithValue("limit", pageSize);
                    dataCmd.Parameters.AddWithValue("offset", (page - 1) * pageSize);

                    await using var r = await dataCmd.ExecuteReaderAsync(ct);
                    while (await r.ReadAsync(ct))
                    {
                        items.Add(new BlacklistSyncItem
                        {
                            Id = r.GetInt64(0),
                            RunId = r.GetGuid(1),
                            SourceType = r.GetString(2),
                            SourceRefId = r.GetString(3),
                            EntityExternalId = r.GetString(4),
                            FullName = r.GetString(5),
                            Action = r.GetString(6),
                            Reason = r.GetString(7),
                            ComparisonResult = r.GetString(8),
                            PreviousDetailsJson = r.IsDBNull(9) ? null : r.GetString(9),
                            CurrentDetailsJson = r.IsDBNull(10) ? null : r.GetString(10),
                            ProcessedAt = r.GetFieldValue<DateTimeOffset>(11)
                        });
                    }
                }

                return new PaginatedSyncItemsResult
                {
                    RunId = runId,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    Items = items
                };
            }
            catch
            {
                // Fallback to memory below
            }
        }

        if (!_memItems.TryGetValue(runId, out var memList))
        {
            return new PaginatedSyncItemsResult
            {
                RunId = runId,
                TotalCount = 0,
                Page = page,
                PageSize = pageSize,
                Items = []
            };
        }

        List<BlacklistSyncItem> snapshot;
        lock (memList)
        {
            snapshot = memList.ToList();
        }

        var query = snapshot.AsEnumerable();
        if (!string.IsNullOrEmpty(act))
            query = query.Where(i => string.Equals(i.Action, act, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(src))
            query = query.Where(i => string.Equals(i.SourceType, src, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(q))
            query = query.Where(i => i.FullName.Contains(q, StringComparison.OrdinalIgnoreCase) || i.EntityExternalId.Contains(q, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(rFilter))
            query = query.Where(i => i.Reason.Contains(rFilter, StringComparison.OrdinalIgnoreCase) || i.ComparisonResult.Contains(rFilter, StringComparison.OrdinalIgnoreCase));

        var filtered = query.ToList();
        var paged = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PaginatedSyncItemsResult
        {
            RunId = runId,
            TotalCount = filtered.Count,
            Page = page,
            PageSize = pageSize,
            Items = paged
        };
    }

    public async Task<IReadOnlyList<BlacklistSyncError>> GetRunErrorsAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await GetRunByIdAsync(runId, ct);
        return run?.Errors ?? [];
    }

    public async Task<BlacklistDistributionDto> GetDistributionStatsAsync(CancellationToken ct = default)
    {
        var sources = await GetSourcesAsync(ct);
        var countsBySource = new Dictionary<string, (int Active, int Inactive)>(StringComparer.OrdinalIgnoreCase)
        {
            ["OFAC"] = (0, 0),
            ["UN"] = (0, 0),
            ["EU"] = (0, 0),
            ["UK"] = (0, 0),
            ["WORLD_CHECK"] = (0, 0)
        };

        bool dbLoaded = false;
        if (HasDatabase)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(ct);

                const string sql = """
                    SELECT
                        COALESCE(source_type,
                            CASE
                                WHEN UPPER(source_list) LIKE 'OFAC%' THEN 'OFAC'
                                WHEN UPPER(source_list) LIKE 'UN%' THEN 'UN'
                                WHEN UPPER(source_list) LIKE 'EU%' THEN 'EU'
                                WHEN UPPER(source_list) LIKE 'UK%' OR UPPER(source_list) LIKE 'OFSI%' THEN 'UK'
                                WHEN UPPER(source_list) LIKE 'WORLD%' THEN 'WORLD_CHECK'
                                ELSE 'OFAC'
                            END
                        ) AS src,
                        COUNT(*) FILTER (WHERE is_active = true)::int AS active_cnt,
                        COUNT(*) FILTER (WHERE is_active = false)::int AS inactive_cnt
                    FROM screening.watchlist_entities
                    GROUP BY 1;
                    """;

                await using var cmd = new NpgsqlCommand(sql, conn);
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    var src = r.GetString(0);
                    var act = r.GetInt32(1);
                    var inact = r.GetInt32(2);
                    if (countsBySource.TryGetValue(src, out var prev))
                    {
                        countsBySource[src] = (prev.Active + act, prev.Inactive + inact);
                    }
                    else
                    {
                        countsBySource[src] = (act, inact);
                    }
                }
                dbLoaded = true;
            }
            catch
            {
                dbLoaded = false;
            }
        }

        if (!dbLoaded)
        {
            foreach (var snap in _memEntities.Values)
            {
                var src = snap.Record.SourceType;
                countsBySource.TryGetValue(src, out var cur);
                countsBySource[src] = snap.IsActive ? (cur.Active + 1, cur.Inactive) : (cur.Active, cur.Inactive + 1);
            }
        }

        int totalActive = countsBySource.Values.Sum(v => v.Active);
        int totalInactive = countsBySource.Values.Sum(v => v.Inactive);

        var slices = new List<BlacklistSourceDistributionSlice>();
        foreach (var srcCfg in sources)
        {
            var (active, inactive) = countsBySource.GetValueOrDefault(srcCfg.SourceType, (srcCfg.ActiveRecordCount, 0));
            decimal pct = totalActive > 0 ? Math.Round((decimal)active / totalActive * 100m, 2) : 0m;
            slices.Add(new BlacklistSourceDistributionSlice
            {
                SourceType = srcCfg.SourceType,
                SourceRefId = srcCfg.SourceRefId,
                DisplayName = srcCfg.DisplayName,
                ActiveCount = active,
                InactiveCount = inactive,
                Percentage = pct,
                LastSyncAt = srcCfg.LastSuccessfulSyncAt
            });
        }

        return new BlacklistDistributionDto
        {
            TotalActiveEntities = totalActive,
            TotalInactiveEntities = totalInactive,
            Sources = slices,
            GeneratedAt = DateTimeOffset.UtcNow
        };
    }

    public async Task<IReadOnlyList<BlacklistDailyTrendPoint>> GetDailyTrendStatsAsync(
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        string? sourceType = null,
        CancellationToken ct = default)
    {
        var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var start = startDate ?? end.AddDays(-13);
        var srcFilter = string.IsNullOrWhiteSpace(sourceType) || string.Equals(sourceType, "ALL", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : sourceType.Trim().ToUpperInvariant();

        var points = new List<BlacklistDailyTrendPoint>();

        if (HasDatabase)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(ct);

                const string sql = """
                    SELECT
                        TO_CHAR(started_at AT TIME ZONE 'UTC', 'YYYY-MM-DD') AS day_utc,
                        CASE WHEN @src = '' THEN 'ALL' ELSE source_type END AS src_label,
                        SUM(total_retrieved)::int,
                        SUM(new_count)::int,
                        SUM(updated_count)::int,
                        SUM(skipped_count)::int,
                        SUM(removed_count)::int,
                        SUM(failed_count)::int
                    FROM screening.blacklist_sync_source_stats
                    WHERE (started_at AT TIME ZONE 'UTC')::date BETWEEN @start AND @end
                      AND (@src = '' OR source_type = @src)
                    GROUP BY 1, 2
                    ORDER BY 1 ASC;
                    """;

                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("start", start);
                cmd.Parameters.AddWithValue("end", end);
                cmd.Parameters.AddWithValue("src", srcFilter);

                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    points.Add(new BlacklistDailyTrendPoint
                    {
                        Date = r.GetString(0),
                        SourceType = r.GetString(1),
                        RetrievedCount = r.GetInt32(2),
                        NewCount = r.GetInt32(3),
                        UpdatedCount = r.GetInt32(4),
                        SkippedCount = r.GetInt32(5),
                        RemovedCount = r.GetInt32(6),
                        FailedCount = r.GetInt32(7)
                    });
                }

                if (points.Count > 0)
                {
                    return points;
                }
            }
            catch
            {
                // Fallback to memory
            }
        }

        var memStats = _memRuns.Values
            .SelectMany(r => r.SourceStats)
            .Where(s =>
            {
                var d = DateOnly.FromDateTime(s.StartedAt.UtcDateTime);
                return d >= start && d <= end && (string.IsNullOrEmpty(srcFilter) || string.Equals(s.SourceType, srcFilter, StringComparison.OrdinalIgnoreCase));
            })
            .GroupBy(s => s.StartedAt.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .OrderBy(g => g.Key)
            .Select(g => new BlacklistDailyTrendPoint
            {
                Date = g.Key,
                SourceType = string.IsNullOrEmpty(srcFilter) ? "ALL" : srcFilter,
                RetrievedCount = g.Sum(x => x.TotalRetrieved),
                NewCount = g.Sum(x => x.NewCount),
                UpdatedCount = g.Sum(x => x.UpdatedCount),
                SkippedCount = g.Sum(x => x.SkippedCount),
                RemovedCount = g.Sum(x => x.RemovedCount),
                FailedCount = g.Sum(x => x.FailedCount)
            })
            .ToList();

        return memStats;
    }

    private static string ComputeRunWormHash(BlacklistSyncRun run)
    {
        var payload = $"{run.Id}|{run.TriggerMode}|{run.TriggeredBy}|{run.StartedAt:O}|{run.CompletedAt:O}|{run.Status}|{run.TotalRetrieved}|{run.TotalNew}|{run.TotalUpdated}|{run.TotalSkipped}|{run.TotalRemoved}|{run.TotalFailed}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private static string GetDefaultRefId(string sourceType) => sourceType.ToUpperInvariant() switch
    {
        "OFAC" => BlacklistSourceDefaults.OfacRefId,
        "UN" => BlacklistSourceDefaults.UnRefId,
        "EU" => BlacklistSourceDefaults.EuRefId,
        "UK" => BlacklistSourceDefaults.UkRefId,
        "WORLD_CHECK" => BlacklistSourceDefaults.WorldCheckRefId,
        _ => BlacklistSourceDefaults.OfacRefId
    };

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    public void Dispose()
    {
        _executionGate.Dispose();
    }

    private sealed record StoredWatchlistSnapshot(
        Guid Id,
        NormalizedBlacklistRecord Record,
        string ContentHash,
        string? AttributesJson,
        bool IsActive);
}
