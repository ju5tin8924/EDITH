using Edith.Screening.Core.Models;
using Edith.Screening.Core.Search;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Edith.Screening.Api.Services;

public sealed partial class WatchlistSeedService : IHostedService
{
    private readonly IWatchlistSearchEngine _searchEngine;
    private readonly ILogger<WatchlistSeedService> _logger;

    public WatchlistSeedService(IWatchlistSearchEngine searchEngine, ILogger<WatchlistSeedService> logger)
    {
        _searchEngine = searchEngine;
        _logger = logger;
    }

    public static IReadOnlyList<WatchlistEntity> BaselineWatchlist { get; } =
    [
        new WatchlistEntity
        {
            Id = Guid.Parse("01931000-0000-7000-8000-000000000001"),
            EntityExternalId = "OFAC-9812",
            EntityType = "INDIVIDUAL",
            FullName = "VIKTOR ANATOLYEVICH BOUT",
            Nationality = "RU",
            Dob = new DateOnly(1967, 1, 13),
            SourceList = "OFAC_SDN",
            Category = "SANCTION",
            IsBlocked = true,
            IsActive = true
        },
        new WatchlistEntity
        {
            Id = Guid.Parse("01931000-0000-7000-8000-000000000002"),
            EntityExternalId = "UN-7721",
            EntityType = "INDIVIDUAL",
            FullName = "TARIQ AZIZ",
            Nationality = "IQ",
            Dob = new DateOnly(1936, 4, 28),
            SourceList = "UN_CONSOLIDATED",
            Category = "SANCTION",
            IsBlocked = true,
            IsActive = true
        },
        new WatchlistEntity
        {
            Id = Guid.Parse("01931000-0000-7000-8000-000000000003"),
            EntityExternalId = "OFAC-CORP-4401",
            EntityType = "CORPORATE",
            FullName = "AL-BARAKAAT TRADING COMPANY",
            Nationality = "SO",
            SourceList = "OFAC_SDN",
            Category = "SANCTION",
            IsBlocked = true,
            IsActive = true
        },
        new WatchlistEntity
        {
            Id = Guid.Parse("01931000-0000-7000-8000-000000000004"),
            EntityExternalId = "OFAC-BANK-0099",
            EntityType = "CORPORATE",
            FullName = "BANK OF BAGHDAD",
            Nationality = "IQ",
            SourceList = "OFAC_SDN",
            Category = "SANCTION",
            IsBlocked = true,
            IsActive = true
        },
        new WatchlistEntity
        {
            Id = Guid.Parse("01931000-0000-7000-8000-000000000005"),
            EntityExternalId = "UN-IND-5501",
            EntityType = "INDIVIDUAL",
            FullName = "DAWOOD IBRAHIM KASKAR",
            Nationality = "IN",
            Dob = new DateOnly(1955, 12, 26),
            SourceList = "UN_CONSOLIDATED",
            Category = "SANCTION",
            IsBlocked = true,
            IsActive = true
        },
        new WatchlistEntity
        {
            Id = Guid.Parse("01931000-0000-7000-8000-000000000006"),
            EntityExternalId = "PEP-UAE-0012",
            EntityType = "INDIVIDUAL",
            FullName = "MANSOOR MOHAMMED AL-MARZOOQI",
            Nationality = "AE",
            SourceList = "LOCAL_PEP_REGISTRY",
            Category = "PEP",
            IsBlocked = false,
            IsActive = true
        }
    ];

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Seeding in-memory watchlist search engine with {Count} baseline entities.")]
    private static partial void LogSeedingStarted(ILogger logger, int count);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Watchlist search engine seeded successfully.")]
    private static partial void LogSeedingCompleted(ILogger logger);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        LogSeedingStarted(_logger, BaselineWatchlist.Count);
        await _searchEngine.IndexBatchAsync(BaselineWatchlist, cancellationToken);
        LogSeedingCompleted(_logger);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
