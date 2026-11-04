using System.Text.Json.Serialization;

namespace Edith.Screening.Bulk.Feeds;

public static class BlacklistSourceDefaults
{
    public const string OfacRefId = "536ca6ad74b9fe781f6a3cd6";
    public const string UnRefId = "538f31c944a9880b5137a07b";
    public const string EuRefId = "538f325c44a9880b5137a07c";
    public const string UkRefId = "5526919db8143b3250b77a02";

    public const string OfacDefaultUrl = "http://www.treasury.gov/ofac/downloads/sdn.xml";
    public const string UnDefaultUrl = "https://scsanctions.un.org/resources/xml/en/consolidated.xml";
    public const string EuDefaultUrl = "https://webgate.ec.europa.eu/fsd/fsf/public/files/xmlFullSanctionsList/content?token=dG9rZW4tMjAxNw";
    public const string UkDefaultUrl = "https://sanctionslist.fcdo.gov.uk/docs/UK-Sanctions-List.xml";

    public static IReadOnlyList<BlacklistSourceConfig> CreateDefaultSources() =>
    [
        new()
        {
            SourceType = "OFAC",
            SourceRefId = OfacRefId,
            DisplayName = "US Treasury OFAC SDN List",
            Url = OfacDefaultUrl,
            IsEnabled = true,
            LastStatus = "NEVER_RUN"
        },
        new()
        {
            SourceType = "UN",
            SourceRefId = UnRefId,
            DisplayName = "UN Security Council Consolidated List",
            Url = UnDefaultUrl,
            IsEnabled = true,
            LastStatus = "NEVER_RUN"
        },
        new()
        {
            SourceType = "EU",
            SourceRefId = EuRefId,
            DisplayName = "European Union Financial Sanctions List (FSF)",
            Url = EuDefaultUrl,
            IsEnabled = true,
            LastStatus = "NEVER_RUN"
        },
        new()
        {
            SourceType = "UK",
            SourceRefId = UkRefId,
            DisplayName = "UK FCDO Consolidated Sanctions List",
            Url = UkDefaultUrl,
            IsEnabled = true,
            LastStatus = "NEVER_RUN"
        }
    ];
}

public sealed record BlacklistSourceConfig
{
    public required string SourceType { get; init; }
    public required string SourceRefId { get; init; }
    public required string DisplayName { get; init; }
    public required string Url { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset? LastSuccessfulSyncAt { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public string LastStatus { get; set; } = "NEVER_RUN";
    public string? LastErrorUserMessage { get; set; }
    public int ActiveRecordCount { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record BlacklistScheduleConfig
{
    public bool IsScheduleEnabled { get; set; } // Initially false until manual update verified
    public string DailyExecutionTimeUtc { get; set; } = "02:00";
    public string Timezone { get; set; } = "UTC";
    public int RetentionDays { get; set; } = 90;
    public DateOnly? LastScheduledRunDate { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string NextScheduledRunUtc
    {
        get
        {
            if (!IsScheduleEnabled)
            {
                return "DISABLED (Manual Verification Gate Active)";
            }

            if (!TimeOnly.TryParse(DailyExecutionTimeUtc, out var time))
            {
                time = new TimeOnly(2, 0);
            }

            var now = DateTimeOffset.UtcNow;
            var candidate = new DateTimeOffset(now.Year, now.Month, now.Day, time.Hour, time.Minute, 0, TimeSpan.Zero);
            if (candidate <= now)
            {
                candidate = candidate.AddDays(1);
            }

            return $"{candidate:yyyy-MM-dd HH:mm} UTC";
        }
    }
}

public sealed record NormalizedBlacklistRecord
{
    public required string SourceType { get; init; }
    public required string SourceRefId { get; init; }
    public required string SourceList { get; init; }
    public required string EntityExternalId { get; init; }
    public required string EntityType { get; init; } // INDIVIDUAL | CORPORATE | VESSEL | AIRCRAFT
    public required string FullName { get; init; }
    public string Category { get; init; } = "SANCTION";
    public IReadOnlyList<string> Aliases { get; init; } = [];
    public IReadOnlyList<string> DatesOfBirth { get; init; } = [];
    public DateOnly? PrimaryDob { get; init; }
    public IReadOnlyList<string> Identifications { get; init; } = [];
    public IReadOnlyList<string> Addresses { get; init; } = [];
    public IReadOnlyList<string> Nationalities { get; init; } = [];
    public IReadOnlyList<string> Programs { get; init; } = [];
    public string? Remarks { get; init; }

    public string? AliasSummary => Aliases.Count > 0 ? string.Join("; ", Aliases) : null;
    public string? IdDetailsSummary => Identifications.Count > 0 ? string.Join("; ", Identifications) : null;
    public string? AddressSummary => Addresses.Count > 0 ? string.Join("; ", Addresses) : null;
    public string? NationalitySummary => Nationalities.Count > 0 ? string.Join("; ", Nationalities) : null;
    public string? ProgramSummary => Programs.Count > 0 ? string.Join("; ", Programs) : null;
}

public sealed record BlacklistSyncRun
{
    public required Guid Id { get; init; }
    public required string TriggerMode { get; init; } // MANUAL | SCHEDULED
    public required string TriggeredBy { get; init; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public long DurationMs { get; set; }
    public string Status { get; set; } = "RUNNING"; // RUNNING | SUCCESS | COMPLETED_WITH_WARNINGS | PARTIALLY_COMPLETED | FAILED
    public string? CurrentSource { get; set; }
    public int SourcesProcessed { get; set; }
    public int SourcesSucceeded { get; set; }
    public int SourcesFailed { get; set; }
    public int TotalRetrieved { get; set; }
    public int TotalNew { get; set; }
    public int TotalUpdated { get; set; }
    public int TotalSkipped { get; set; }
    public int TotalRemoved { get; set; }
    public int TotalFailed { get; set; }
    public string? UserSummaryMessage { get; set; }
    public string? WormHash { get; set; }
    public List<BlacklistSourceStat> SourceStats { get; set; } = [];
    public List<BlacklistSyncError> Errors { get; set; } = [];
}

public sealed record BlacklistSourceStat
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid RunId { get; init; }
    public required string SourceType { get; init; }
    public required string SourceRefId { get; init; }
    public required string SourceUrl { get; init; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public long DurationMs { get; set; }
    public int AttemptCount { get; set; } = 1;
    public string Status { get; set; } = "RUNNING"; // RUNNING | SUCCESS | COMPLETED_WITH_WARNINGS | FAILED | DISABLED
    public int TotalRetrieved { get; set; }
    public int NewCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public int RemovedCount { get; set; }
    public int FailedCount { get; set; }
    public string? UserErrorMessage { get; set; }
    public string? TechnicalErrorDetails { get; set; }
}

public sealed record BlacklistSyncItem
{
    public long Id { get; set; }
    public required Guid RunId { get; init; }
    public required string SourceType { get; init; }
    public required string SourceRefId { get; init; }
    public required string EntityExternalId { get; init; }
    public required string FullName { get; init; }
    public required string Action { get; init; } // NEW | UPDATED | SKIPPED | REMOVED | FAILED
    public required string Reason { get; init; }
    public required string ComparisonResult { get; init; }
    public string? PreviousDetailsJson { get; init; }
    public string? CurrentDetailsJson { get; init; }
    public DateTimeOffset ProcessedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record BlacklistSyncError
{
    public long Id { get; set; }
    public required Guid RunId { get; init; }
    public required string SourceType { get; init; }
    public required string ErrorCategory { get; init; }
    public required string UserMessage { get; init; }
    public required string TechnicalDetails { get; init; }
    public string? AffectedRecordId { get; init; }
    public bool IsRecoverable { get; init; } = true;
    public bool RetryAttempted { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record UrlValidationResultDto(
    bool IsValid,
    string NormalizedUrl,
    string UserMessage);

public sealed record SourceTestResultDto
{
    public required string SourceType { get; init; }
    public required string SourceRefId { get; init; }
    public required string TestedUrl { get; init; }
    public required bool IsReachable { get; init; }
    public required bool IsXmlValid { get; init; }
    public int HttpStatusCode { get; init; }
    public long LatencyMs { get; init; }
    public string? RootElementName { get; init; }
    public int SampleRecordCount { get; init; }
    public IReadOnlyList<NormalizedBlacklistRecord> SampleRecords { get; init; } = [];
    public required string UserMessage { get; init; }
    public string? TechnicalDetails { get; init; }
}

public sealed record PaginatedSyncItemsResult
{
    public required Guid RunId { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required IReadOnlyList<BlacklistSyncItem> Items { get; init; }
}

public sealed record BlacklistSourceDistributionSlice
{
    public required string SourceType { get; init; }
    public required string SourceRefId { get; init; }
    public required string DisplayName { get; init; }
    public required int ActiveCount { get; init; }
    public required int InactiveCount { get; init; }
    public required decimal Percentage { get; init; }
    public DateTimeOffset? LastSyncAt { get; init; }
}

public sealed record BlacklistDistributionDto
{
    public required int TotalActiveEntities { get; init; }
    public required int TotalInactiveEntities { get; init; }
    public required IReadOnlyList<BlacklistSourceDistributionSlice> Sources { get; init; }
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record BlacklistDailyTrendPoint
{
    public required string Date { get; init; } // YYYY-MM-DD
    public required string SourceType { get; init; } // ALL | OFAC | UN | EU | UK
    public int RetrievedCount { get; set; }
    public int NewCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public int RemovedCount { get; set; }
    public int FailedCount { get; set; }
}

public sealed record BlacklistLiveStatusDto
{
    public required bool IsRunning { get; init; }
    public string? CurrentSource { get; init; }
    public BlacklistSyncRun? ActiveOrLatestRun { get; init; }
    public required BlacklistScheduleConfig ScheduleConfig { get; init; }
    public required IReadOnlyList<BlacklistSourceConfig> Sources { get; init; }
}
