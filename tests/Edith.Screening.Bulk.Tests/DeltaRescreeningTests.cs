using Edith.Screening.Bulk.Delta;
using Edith.Screening.Core.Models;
using Edith.Screening.Core.Normalization;
using Edith.Screening.Core.Scoring;

namespace Edith.Screening.Bulk.Tests;

public sealed class DeltaRescreeningTests
{
    private static readonly Guid TestTenantId = Guid.Parse("01931000-0000-7000-8000-000000000001");
    private readonly TextNormalizer _normalizer = new();
    private readonly NameSimilarityScorer _nameScorer = new();

    private static ScoreConfig CreateConfig(decimal threshold) => new()
    {
        NameWeightage = 60m,
        NationalityWeightage = 15m,
        DobWeightage = 15m,
        IdWeightage = 10m,
        MinThresholdScore = threshold
    };

    [Fact]
    public async Task DeltaRescreening_Applies_Nationality_Bonus_And_Threshold_Filtering()
    {
        var deltaService = new DeltaRescreeningService(_normalizer, _nameScorer);

        var customers = new List<CustomerProfile>
        {
            new() { Id = Guid.NewGuid(), TenantId = TestTenantId, CustomerCode = "CUST-MATCH", FullName = "Victor Bout", Nationality = "RU" },
            new() { Id = Guid.NewGuid(), TenantId = TestTenantId, CustomerCode = "CUST-DIFF-NAT", FullName = "Victor Bout", Nationality = "US" }
        };

        var deltaWatchlist = new List<WatchlistEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                EntityExternalId = "OFAC-9812",
                EntityType = "INDIVIDUAL",
                FullName = "VIKTOR ANATOLYEVICH BOUT",
                Nationality = "RU",
                SourceList = "OFAC_SDN",
                Category = "SANCTION",
                IsBlocked = true
            }
        };

        var hits = await deltaService.ExecuteDeltaRescreenAsync(TestTenantId, customers, deltaWatchlist, CreateConfig(80m));

        Assert.NotEmpty(hits);
        var matchNat = hits.FirstOrDefault(h => h.Customer.CustomerCode == "CUST-MATCH");
        var diffNat = hits.FirstOrDefault(h => h.Customer.CustomerCode == "CUST-DIFF-NAT");

        Assert.NotNull(matchNat);
        if (diffNat != null)
        {
            // Matching nationality should have a score >= differing nationality
            Assert.True(matchNat.CompositeScore >= diffNat.CompositeScore);
        }
    }

    [Fact]
    public async Task DeltaRescreening_Returns_Empty_When_Watchlist_Has_No_Entities()
    {
        var deltaService = new DeltaRescreeningService(_normalizer, _nameScorer);

        var customers = new List<CustomerProfile>
        {
            new() { Id = Guid.NewGuid(), TenantId = TestTenantId, CustomerCode = "CUST-001", FullName = "Rashid Al-Nuaimi", Nationality = "AE" }
        };

        var hits = await deltaService.ExecuteDeltaRescreenAsync(TestTenantId, customers, []);

        Assert.Empty(hits);
    }

    [Fact]
    public async Task DeltaRescreening_Filters_Below_Threshold()
    {
        var deltaService = new DeltaRescreeningService(_normalizer, _nameScorer);

        var customers = new List<CustomerProfile>
        {
            new() { Id = Guid.NewGuid(), TenantId = TestTenantId, CustomerCode = "CUST-CLEAN", FullName = "Johnathan David Smith", Nationality = "US" }
        };

        var deltaWatchlist = new List<WatchlistEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                EntityExternalId = "OFAC-9812",
                EntityType = "INDIVIDUAL",
                FullName = "VIKTOR ANATOLYEVICH BOUT",
                Nationality = "RU",
                SourceList = "OFAC_SDN",
                Category = "SANCTION",
                IsBlocked = true
            }
        };

        // Strict threshold of 85%
        var hits = await deltaService.ExecuteDeltaRescreenAsync(TestTenantId, customers, deltaWatchlist, CreateConfig(85m));

        Assert.Empty(hits);
    }
}
