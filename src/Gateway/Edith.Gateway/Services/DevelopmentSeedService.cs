using Edith.Cases.Models;
using Edith.Cases.Services;
using Edith.Rules.Models;
using Edith.Rules.Repository;
using Edith.Screening.Api.Services;
using Edith.Screening.Core.Models;
using Edith.Screening.Core.Search;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Edith.Gateway.Services;

public sealed partial class DevelopmentSeedService : IHostedService
{
    private static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly IWatchlistSearchEngine _searchEngine;
    private readonly ICaseService _caseService;
    private readonly IRuleRepository _ruleRepo;
    private readonly IWhitelistService _whitelistService;
    private readonly ILogger<DevelopmentSeedService> _logger;

    public DevelopmentSeedService(
        IWatchlistSearchEngine searchEngine,
        ICaseService caseService,
        IRuleRepository ruleRepo,
        IWhitelistService whitelistService,
        ILogger<DevelopmentSeedService> logger)
    {
        _searchEngine = searchEngine;
        _caseService = caseService;
        _ruleRepo = ruleRepo;
        _whitelistService = whitelistService;
        _logger = logger;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Seeding EDITH development stores (Watchlist, Cases, Rules, Whitelist)...")]
    private static partial void LogSeedingStarted(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "EDITH development seed completed successfully.")]
    private static partial void LogSeedingCompleted(ILogger logger);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        LogSeedingStarted(_logger);

        // 1. Seed Watchlist
        await _searchEngine.IndexBatchAsync(WatchlistSeedService.BaselineWatchlist, cancellationToken);

        // 2. Seed Baseline Rules
        await _ruleRepo.CreateRuleAsync(new AmlRule
        {
            Id = Guid.Parse("01931000-0000-7000-8000-000000000101"),
            TenantId = DefaultTenantId,
            RuleCode = "RULE_FATF_GREY_BLACK",
            RuleName = "FATF High Risk & Monitored Corridors",
            Category = RuleCategory.HighRiskCorridor,
            RiskLevel = RiskLevel.Critical,
            ScoreWeightage = 45.0m,
            IsActive = true,
            HighRiskCountries = ["IR", "KP", "MM", "SY", "RU", "YE"],
            ThresholdAmount = 0m,
            Currency = "AED"
        }, cancellationToken);

        await _ruleRepo.CreateRuleAsync(new AmlRule
        {
            Id = Guid.Parse("01931000-0000-7000-8000-000000000102"),
            TenantId = DefaultTenantId,
            RuleCode = "RULE_STRUCTURING_AED",
            RuleName = "Structuring Smurfing Window (UAE CBUAE)",
            Category = RuleCategory.StructuringSmurfing,
            RiskLevel = RiskLevel.High,
            ScoreWeightage = 35.0m,
            IsActive = true,
            WindowMinutes = 1440,
            MinTransactions = 3,
            StructuringMinAmount = 45000m,
            StructuringMaxAmount = 54999m,
            Currency = "AED"
        }, cancellationToken);

        await _ruleRepo.CreateRuleAsync(new AmlRule
        {
            Id = Guid.Parse("01931000-0000-7000-8000-000000000103"),
            TenantId = DefaultTenantId,
            RuleCode = "RULE_CASH_THRESHOLD_50K",
            RuleName = "Single Large Value Transfer Threshold",
            Category = RuleCategory.TransactionThreshold,
            RiskLevel = RiskLevel.Medium,
            ScoreWeightage = 20.0m,
            IsActive = true,
            ThresholdAmount = 500000m,
            Currency = "AED"
        }, cancellationToken);

        // 3. Seed Baseline Cases
        var case1 = new CaseDetail
        {
            Id = Guid.Parse("c1111111-2222-3333-4444-555555555555"),
            TenantId = DefaultTenantId,
            CaseNumber = "EDITH-CAS-2026-0042",
            TransactionId = Guid.NewGuid(),
            TransactionRefNo = "TX-2026-9921448",
            Amount = 450000.00m,
            Currency = "USD",
            SenderName = "Acme Global Trading LLC",
            BeneficiaryName = "Viktor Anatolyevich Bout",
            RiskScore = 94.5m,
            Priority = CasePriority.Critical,
            Status = CaseStatus.Open,
            TriggeredList = "UN Security Council Res 1521 / OFAC SDN",
            HoldReason = "Specially Designated National under Executive Order 13348",
            SlaDueDate = DateTimeOffset.UtcNow.AddHours(68),
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-4)
        };
        await _caseService.CreateCaseAsync(case1, cancellationToken);

        var case2 = new CaseDetail
        {
            Id = Guid.Parse("c2222222-3333-4444-5555-666666666666"),
            TenantId = DefaultTenantId,
            CaseNumber = "EDITH-CAS-2026-0043",
            TransactionId = Guid.NewGuid(),
            TransactionRefNo = "TX-2026-9921449",
            Amount = 850000.00m,
            Currency = "AED",
            SenderName = "Middle East Commodity Brokers",
            BeneficiaryName = "Al-Barakaat Trading Company",
            RiskScore = 82.0m,
            Priority = CasePriority.High,
            Status = CaseStatus.UnderReview,
            TriggeredList = "OFAC SDN / Terrorist Financing List",
            HoldReason = "Corporate SDN match with high token overlap",
            SlaDueDate = DateTimeOffset.UtcNow.AddHours(60),
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-12),
            LockedBy = Guid.Parse("01931000-0000-7000-8000-000000000020"),
            LockedByName = "Tariq Mansoor",
            LockedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            LockExpiresAt = DateTimeOffset.UtcNow.AddMinutes(90)
        };
        await _caseService.CreateCaseAsync(case2, cancellationToken);

        var case3 = new CaseDetail
        {
            Id = Guid.Parse("c3333333-4444-5555-6666-777777777777"),
            TenantId = DefaultTenantId,
            CaseNumber = "EDITH-CAS-2026-0044",
            TransactionId = Guid.NewGuid(),
            TransactionRefNo = "TX-2026-9921450",
            Amount = 1200000.00m,
            Currency = "AED",
            SenderName = "Global Logistics Network",
            BeneficiaryName = "Mansoor Mohammed Al-Marzooqi",
            RiskScore = 68.0m,
            Priority = CasePriority.Medium,
            Status = CaseStatus.PendingApproval,
            TriggeredList = "LOCAL_PEP_REGISTRY",
            HoldReason = "Politically Exposed Person (Tier 2 Ministerial Advisor)",
            SlaDueDate = DateTimeOffset.UtcNow.AddHours(48),
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-24)
        };
        await _caseService.CreateCaseAsync(case3, cancellationToken);

        // 4. Seed Whitelist
        await _whitelistService.AddWhitelistAsync(new WhitelistEntry
        {
            Id = Guid.NewGuid(),
            TenantId = DefaultTenantId,
            EntityIdentifier = "EMIRATES AIRLINE",
            MatchScope = "EXACT",
            Reason = "National Carrier State-Owned Exemption",
            ExpiresAt = DateTimeOffset.UtcNow.AddYears(1),
            IsActive = true
        }, cancellationToken);

        LogSeedingCompleted(_logger);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
