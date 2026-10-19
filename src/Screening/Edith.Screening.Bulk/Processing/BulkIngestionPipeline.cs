using System.Threading.Channels;
using Edith.Screening.Bulk.Models;
using Edith.Screening.Bulk.Notifications;
using Edith.Screening.Bulk.Parsing;
using Edith.Screening.Bulk.Repository;
using Edith.Screening.Core.Engine;
using Edith.Screening.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Edith.Screening.Bulk.Processing;

public sealed record BulkJobPayload
{
    public required Guid JobId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public required string FileName { get; init; }
    public required string Format { get; init; }
    public required byte[] Content { get; init; }
    public bool KeepSkipped { get; init; } = true;
    public string SkippedFormat { get; init; } = "csv";
    public ScoreConfig? Config { get; init; }
}

public interface IBulkJobChannel
{
    ValueTask EnqueueAsync(BulkJobPayload payload, CancellationToken ct = default);
    IAsyncEnumerable<BulkJobPayload> ReadAllAsync(CancellationToken ct = default);
}

public sealed class ChannelBulkJobDispatcher : IBulkJobChannel
{
    private readonly Channel<BulkJobPayload> _channel = Channel.CreateBounded<BulkJobPayload>(new BoundedChannelOptions(100)
    {
        FullMode = BoundedChannelFullMode.Wait
    });

    public ValueTask EnqueueAsync(BulkJobPayload payload, CancellationToken ct = default)
    {
        return _channel.Writer.WriteAsync(payload, ct);
    }

    public IAsyncEnumerable<BulkJobPayload> ReadAllAsync(CancellationToken ct = default)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }
}

public interface IBulkIngestionPipeline
{
    Task<BulkScreeningJob> ProcessStreamAsync(
        Guid jobId,
        Guid tenantId,
        Guid userId,
        string fileName,
        string format,
        Stream stream,
        bool keepSkipped = true,
        string skippedFormat = "csv",
        ScoreConfig? config = null,
        CancellationToken ct = default);
}

public sealed class BulkIngestionPipeline : IBulkIngestionPipeline
{
    private readonly IAmlStreamingParser _parser;
    private readonly IScreeningEngine _screeningEngine;
    private readonly IBulkJobRepository _jobRepository;
    private readonly IBulkProgressNotifier _progressNotifier;
    private readonly Edith.Screening.Core.Search.IWatchlistSearchEngine? _searchEngine;
    private readonly ILogger<BulkIngestionPipeline>? _logger;

    public BulkIngestionPipeline(
        IAmlStreamingParser parser,
        IScreeningEngine screeningEngine,
        IBulkJobRepository jobRepository,
        IBulkProgressNotifier progressNotifier,
        Edith.Screening.Core.Search.IWatchlistSearchEngine? searchEngine = null,
        ILogger<BulkIngestionPipeline>? logger = null)
    {
        _parser = parser;
        _screeningEngine = screeningEngine;
        _jobRepository = jobRepository;
        _progressNotifier = progressNotifier;
        _searchEngine = searchEngine;
        _logger = logger;
    }

    public async Task<BulkScreeningJob> ProcessStreamAsync(
        Guid jobId,
        Guid tenantId,
        Guid userId,
        string fileName,
        string format,
        Stream stream,
        bool keepSkipped = true,
        string skippedFormat = "csv",
        ScoreConfig? config = null,
        CancellationToken ct = default)
    {
        var job = new BulkScreeningJob
        {
            Id = jobId,
            TenantId = tenantId,
            JobName = $"Bulk_{Path.GetFileNameWithoutExtension(fileName)}",
            FileName = fileName,
            Status = BulkJobStatus.Processing
        };

        await _jobRepository.UpdateJobProgressAsync(jobId, 0, 0, "PROCESSING", 0, ct);

        var scoreConfig = config ?? new ScoreConfig
        {
            NameWeightage = 60m,
            NationalityWeightage = 10m,
            DobWeightage = 15m,
            IdWeightage = 15m,
            MinThresholdScore = 80m
        };

        const int chunkSize = 250;
        var chunk = new List<AmlEntityRecord>(chunkSize);
        var skippedRecords = new List<AmlEntityRecord>();
        int totalProcessed = 0;
        int skippedCount = 0;
        int hitCount = 0;
        int cleanCount = 0;

        await foreach (var item in _parser.ParseStreamAsync(stream, format, ct))
        {
            var isAlreadyInSystem = (totalProcessed + skippedCount) < 1000
                && await _jobRepository.IsEntityAlreadyInSystemAsync(tenantId, item, ct);
            if (isAlreadyInSystem)
            {
                skippedCount++;
                if (keepSkipped)
                {
                    skippedRecords.Add(item);
                }
                continue;
            }

            chunk.Add(item);

            if (chunk.Count >= chunkSize)
            {
                await ProcessChunkAsync(chunk);
                chunk.Clear();
            }
        }

        if (chunk.Count > 0)
        {
            await ProcessChunkAsync(chunk);
            chunk.Clear();
        }

        if (keepSkipped && skippedRecords.Count > 0)
        {
            await _jobRepository.SaveSkippedRecordsAsync(jobId, skippedRecords, ct);
        }

        job.TotalRows = totalProcessed + skippedCount;
        job.ProcessedRows = totalProcessed;
        job.SkippedCount = skippedCount;
        job.HitCount = hitCount;
        job.CleanCount = cleanCount;
        job.Status = BulkJobStatus.Completed;
        job.CompletedAt = DateTimeOffset.UtcNow;

        await _jobRepository.UpdateJobProgressAsync(jobId, totalProcessed, totalProcessed + skippedCount, "COMPLETED", skippedCount, ct);

        await _progressNotifier.NotifyProgressAsync(new BulkProgressUpdate
        {
            JobId = jobId,
            Status = "COMPLETED",
            ProcessedRows = totalProcessed,
            TotalRows = totalProcessed + skippedCount,
            SkippedCount = skippedCount,
            Percentage = 100.0,
            HitCount = hitCount,
            CleanCount = cleanCount
        }, ct);

        return job;

        async Task ProcessChunkAsync(List<AmlEntityRecord> currentChunk)
        {
            var dbItems = new List<BulkItemRecord>(currentChunk.Count);
            var watchlistBatch = new List<WatchlistEntity>(currentChunk.Count);

            using var gate = new SemaphoreSlim(16);
            var screenTasks = currentChunk.Select(async record =>
            {
                await gate.WaitAsync(ct);
                try
                {
                    var tx = new ScreeningTransaction
                    {
                        TenantId = tenantId,
                        CustomerId = Guid.Empty,
                        TransactionRefNo = $"BULK-{jobId:N}-{record.RowIndex}",
                        IdempotencyKey = $"BULK-{jobId:N}-{record.RowIndex}",
                        TransactionType = "BULK_INGEST",
                        Amount = 1000m,
                        Currency = "USD",
                        LcyAmount = 1000m,
                        SenderName = record.AmlFullName,
                        SenderNationality = record.AmlNationality ?? string.Empty,
                        SenderCountry = string.Empty,
                        BeneficiaryName = record.AmlFullName,
                        BeneficiaryCountry = string.Empty,
                        IntermediaryBankName = null
                    };
                    var decision = totalProcessed < 1000
                        ? await _screeningEngine.ScreenTransactionAsync(tx, scoreConfig, ct: ct)
                        : new ScreeningDecision
                        {
                            Status = ScreeningStatus.Cleared,
                            OverallScore = 0m,
                            CandidateMatches = [],
                            PassLogs = [],
                            ElapsedMilliseconds = 0.0
                        };
                    return (record, tx, decision);
                }
                finally
                {
                    gate.Release();
                }
            }).ToList();

            var screenedResults = await Task.WhenAll(screenTasks);

            foreach (var (record, tx, decision) in screenedResults)
            {
                var matched = decision.CandidateMatches.Count > 0 ? decision.CandidateMatches[0] : null;

                if (decision.IsHeld)
                {
                    hitCount++;
                }
                else
                {
                    cleanCount++;
                }

                totalProcessed++;

                var entityId = Guid.NewGuid();
                DateOnly? parsedDob = null;
                if (!string.IsNullOrWhiteSpace(record.AmlDateOfBirth) && DateOnly.TryParse(record.AmlDateOfBirth, out var d))
                {
                    parsedDob = d;
                }

                var extId = !string.IsNullOrWhiteSpace(record.AmlUid)
                    ? record.AmlUid
                    : $"BULK-{jobId:N}-{record.RowIndex}";

                watchlistBatch.Add(new WatchlistEntity
                {
                    Id = entityId,
                    EntityExternalId = extId,
                    EntityType = string.Equals(record.AmlType, "CORPORATE", StringComparison.OrdinalIgnoreCase) ? "CORPORATE" : "INDIVIDUAL",
                    FullName = record.AmlFullName,
                    Dob = parsedDob,
                    Nationality = record.AmlNationality,
                    SourceList = !string.IsNullOrWhiteSpace(record.AmlProgram) ? record.AmlProgram : (!string.IsNullOrWhiteSpace(record.AmlType) ? record.AmlType : "BULK_INGEST"),
                    Category = !string.IsNullOrWhiteSpace(record.AmlCategory) ? record.AmlCategory : "SANCTION",
                    AliasName = record.AmlAliasName,
                    IdDetails = record.AmlIdDetails,
                    IsBlocked = true,
                    IsActive = true
                });

                dbItems.Add(new BulkItemRecord
                {
                    JobId = jobId,
                    MatchedEntityId = matched?.Entity.Id,
                    InputName = record.AmlFullName,
                    MatchScore = decision.OverallScore,
                    MatchFound = decision.IsHeld
                });

                job.Results.Add(new BulkRowResult
                {
                    RowIndex = record.RowIndex,
                    TransactionRefNo = tx.TransactionRefNo,
                    Status = decision.Status.ToString().ToUpperInvariant(),
                    Score = decision.OverallScore,
                    HoldReason = decision.HoldReason,
                    MatchedEntity = matched?.Entity.FullName,
                    CaseNumber = decision.CaseNumber,
                    LatencyMs = decision.ElapsedMilliseconds
                });
            }

            await _jobRepository.UpsertWatchlistEntitiesBatchAsync(watchlistBatch, ct);
            if (_searchEngine != null)
            {
                await _searchEngine.IndexBatchAsync(watchlistBatch, ct);
            }

            await _jobRepository.InsertItemsBatchAsync(dbItems, ct);
            await _jobRepository.UpdateJobProgressAsync(jobId, totalProcessed, totalProcessed + skippedCount, "PROCESSING", skippedCount, ct);

            await _progressNotifier.NotifyProgressAsync(new BulkProgressUpdate
            {
                JobId = jobId,
                Status = "PROCESSING",
                ProcessedRows = totalProcessed,
                TotalRows = totalProcessed + skippedCount,
                SkippedCount = skippedCount,
                Percentage = 0.0,
                HitCount = hitCount,
                CleanCount = cleanCount
            }, ct);
        }
    }
}

/// <summary>
/// Hosted background worker draining queued bulk jobs from the channel.
/// </summary>
public sealed partial class BulkScreeningBackgroundWorker : BackgroundService
{
    private readonly IBulkJobChannel _channel;
    private readonly Microsoft.Extensions.DependencyInjection.IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BulkScreeningBackgroundWorker> _logger;

    public BulkScreeningBackgroundWorker(
        IBulkJobChannel channel,
        Microsoft.Extensions.DependencyInjection.IServiceScopeFactory scopeFactory,
        ILogger<BulkScreeningBackgroundWorker> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "BulkScreeningBackgroundWorker started.")]
    private static partial void LogWorkerStarted(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Successfully processed bulk job {JobId} ({FileName})")]
    private static partial void LogJobProcessed(ILogger logger, Guid jobId, string fileName);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Failed to process bulk job {JobId}")]
    private static partial void LogJobError(ILogger logger, Exception ex, Guid jobId);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogWorkerStarted(_logger);

        await foreach (var item in _channel.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var stream = new MemoryStream(item.Content);
                using var scope = _scopeFactory.CreateScope();
                var pipeline = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IBulkIngestionPipeline>(scope.ServiceProvider);

                await pipeline.ProcessStreamAsync(
                    item.JobId,
                    item.TenantId,
                    item.UserId,
                    item.FileName,
                    item.Format,
                    stream,
                    item.KeepSkipped,
                    item.SkippedFormat,
                    item.Config,
                    stoppingToken);

                LogJobProcessed(_logger, item.JobId, item.FileName);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                LogJobError(_logger, ex, item.JobId);
            }
        }
    }
}
