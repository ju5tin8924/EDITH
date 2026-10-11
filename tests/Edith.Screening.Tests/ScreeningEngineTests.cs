using Edith.Screening.Core.Engine;
using Edith.Screening.Core.Models;
using Edith.Screening.Core.Normalization;
using Edith.Screening.Core.Scoring;
using Edith.Screening.Core.Search;

namespace Edith.Screening.Tests;

public class ScreeningEngineTests : IDisposable
{
    private readonly TextNormalizer _normalizer = new();
    private readonly InMemoryWatchlistSearchEngine _searchEngine;
    private readonly NameSimilarityScorer _nameScorer = new();
    private readonly ScreeningEngine _engine;

    private readonly ScoreConfig _defaultConfig = new()
    {
        NameWeightage = 60.00m,
        NationalityWeightage = 10.00m,
        DobWeightage = 15.00m,
        IdWeightage = 15.00m,
        MinThresholdScore = 80.00m
    };

    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly Guid _customerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public ScreeningEngineTests()
    {
        _searchEngine = new InMemoryWatchlistSearchEngine(_normalizer);
        _engine = new ScreeningEngine(_normalizer, _searchEngine, _nameScorer);

        // Seed sample sanctions into in-memory search index
        _searchEngine.IndexBatchAsync([
            new WatchlistEntity
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                EntityExternalId = "OFAC-SDN-101",
                EntityType = "INDIVIDUAL",
                FullName = "SADDAM HUSSEIN",
                Nationality = "IRQ",
                SourceList = "OFAC_SDN",
                Category = "SANCTION",
                IsBlocked = true
            },
            new WatchlistEntity
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                EntityExternalId = "UN-CON-202",
                EntityType = "INDIVIDUAL",
                FullName = "MOHAMMED AL-HASHIMI",
                Nationality = "SAU",
                SourceList = "UN_CONSOLIDATED",
                Category = "PEP",
                IsBlocked = false
            },
            new WatchlistEntity
            {
                Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                EntityExternalId = "OFAC-CORP-303",
                EntityType = "CORPORATE",
                FullName = "BANK OF SADERAT TRADING COMPANY",
                Nationality = "IRN",
                SourceList = "OFAC_SDN",
                Category = "SANCTION",
                IsBlocked = true
            }
        ]).Wait();
    }

    public void Dispose()
    {
        _searchEngine.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Exact_sanction_match_on_beneficiary_holds_transaction()
    {
        var tx = new ScreeningTransaction
        {
            TenantId = _tenantId,
            CustomerId = _customerId,
            TransactionRefNo = "TX-2026-0001",
            IdempotencyKey = "IDEMP-KEY-0001",
            TransactionType = "OUTWARD_REMITTANCE",
            Amount = 50000m,
            Currency = "USD",
            LcyAmount = 183500m,
            SenderName = "AHMED AL-MANSOOR",
            SenderNationality = "ARE",
            SenderCountry = "ARE",
            BeneficiaryName = "SADDAM HUSSEIN",
            BeneficiaryCountry = "IRQ"
        };

        var decision = await _engine.ScreenTransactionAsync(tx, _defaultConfig);

        Assert.Equal(ScreeningStatus.Held, decision.Status);
        Assert.True(decision.IsHeld);
        Assert.True(decision.OverallScore >= 95.00m);
        Assert.NotNull(decision.CaseNumber);
        Assert.StartsWith("CASE-", decision.CaseNumber);
        Assert.Contains("SANCTION_MATCH", decision.HoldReason);
        Assert.Equal("OFAC_SDN", decision.TriggeredList);
        Assert.Equal(5, decision.PassLogs.Count);
        Assert.True(decision.ElapsedMilliseconds >= 0);
    }

    [Fact]
    public async Task Fuzzy_match_above_threshold_holds_transaction()
    {
        var tx = new ScreeningTransaction
        {
            TenantId = _tenantId,
            CustomerId = _customerId,
            TransactionRefNo = "TX-2026-0002",
            IdempotencyKey = "IDEMP-KEY-0002",
            TransactionType = "CROSS_BORDER_WIRE",
            Amount = 15000m,
            Currency = "USD",
            LcyAmount = 55050m,
            SenderName = "MOHAMED AL HASHMI", // Fuzzy variant of MOHAMMED AL-HASHIMI
            SenderNationality = "SAU",
            SenderCountry = "SAU",
            BeneficiaryName = "TARIQ OMAR",
            BeneficiaryCountry = "ARE"
        };

        var decision = await _engine.ScreenTransactionAsync(tx, _defaultConfig);

        Assert.Equal(ScreeningStatus.Held, decision.Status);
        Assert.True(decision.OverallScore >= _defaultConfig.MinThresholdScore);
        Assert.Contains("PEP_MATCH", decision.HoldReason);
    }

    [Fact]
    public async Task Clear_transaction_with_no_hits_returns_cleared()
    {
        var tx = new ScreeningTransaction
        {
            TenantId = _tenantId,
            CustomerId = _customerId,
            TransactionRefNo = "TX-2026-0003",
            IdempotencyKey = "IDEMP-KEY-0003",
            TransactionType = "DOMESTIC_TRANSFER",
            Amount = 2500m,
            Currency = "AED",
            LcyAmount = 2500m,
            SenderName = "KHALID NOOR",
            SenderNationality = "ARE",
            SenderCountry = "ARE",
            BeneficiaryName = "RASHID BUTTI",
            BeneficiaryCountry = "ARE"
        };

        var decision = await _engine.ScreenTransactionAsync(tx, _defaultConfig);

        Assert.Equal(ScreeningStatus.Cleared, decision.Status);
        Assert.False(decision.IsHeld);
        Assert.Null(decision.HoldReason);
        Assert.Null(decision.CaseNumber);
        Assert.Equal(5, decision.PassLogs.Count);
    }

    [Fact]
    public async Task Active_whitelist_suppresses_hold_and_clears_transaction()
    {
        var tx = new ScreeningTransaction
        {
            TenantId = _tenantId,
            CustomerId = _customerId,
            TransactionRefNo = "TX-2026-0004",
            IdempotencyKey = "IDEMP-KEY-0004",
            TransactionType = "TRADE_FINANCE",
            Amount = 100000m,
            Currency = "USD",
            LcyAmount = 367000m,
            SenderName = "GULF OIL TRADING",
            SenderNationality = "ARE",
            SenderCountry = "ARE",
            BeneficiaryName = "SADDAM HUSSEIN", // Sanctioned name
            BeneficiaryCountry = "IRQ"
        };

        var whitelists = new List<WhitelistEntry>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                CustomerId = _customerId,
                EntityIdentifier = "SADDAM HUSSEIN",
                MatchScope = "EXACT",
                Reason = "Court-authorized settlement receiver - AML exemption #8812",
                IsActive = true,
                ExpiresAt = DateTimeOffset.UtcNow.AddYears(1)
            }
        };

        var decision = await _engine.ScreenTransactionAsync(tx, _defaultConfig, whitelists);

        Assert.Equal(ScreeningStatus.Cleared, decision.Status);
        Assert.False(decision.IsHeld);
        Assert.True(decision.IsWhitelisted);
        Assert.Contains("CLEARED_BY_WHITELIST", decision.HoldReason);
        Assert.Contains("Court-authorized settlement", decision.HoldReason);
    }

    [Fact]
    public async Task Sanctioned_intermediary_bank_triggers_hold_even_with_clean_parties()
    {
        var tx = new ScreeningTransaction
        {
            TenantId = _tenantId,
            CustomerId = _customerId,
            TransactionRefNo = "TX-2026-0005",
            IdempotencyKey = "IDEMP-KEY-0005",
            TransactionType = "SWIFT_MT103",
            Amount = 75000m,
            Currency = "EUR",
            LcyAmount = 300000m,
            SenderName = "CLEAR SENDER CORP",
            SenderNationality = "GBR",
            SenderCountry = "GBR",
            BeneficiaryName = "CLEAR BENEFICIARY LTD",
            BeneficiaryCountry = "ARE",
            IntermediaryBankName = "BANK OF SADERAT TRADING COMPANY" // Sanctioned corporate entity
        };

        var decision = await _engine.ScreenTransactionAsync(tx, _defaultConfig);

        Assert.Equal(ScreeningStatus.Held, decision.Status);
        Assert.True(decision.IsHeld);
        Assert.Contains("IntermediaryBank", decision.HoldReason);
    }
}
