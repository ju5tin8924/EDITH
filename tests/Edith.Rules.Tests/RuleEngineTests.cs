using Edith.Rules.Engine;
using Edith.Rules.Models;
using Edith.Rules.Repository;
using Edith.Rules.Sandbox;
using Edith.Screening.Core.Models;

namespace Edith.Rules.Tests;

public sealed class RuleEngineTests
{
    private static readonly Guid TestTenantId = Guid.Parse("01931000-0000-7000-8000-000000000001");

    [Fact]
    public async Task Rule_CRUD_Operates_Correctly_For_Tenant()
    {
        var repo = new InMemoryRuleRepository();

        var rule = new AmlRule
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            RuleCode = "RULE-AE-LCTR-001",
            RuleName = "Large Cash Transaction Reporting >= 55,000 AED",
            Category = RuleCategory.TransactionThreshold,
            RiskLevel = RiskLevel.High,
            ScoreWeightage = 40m,
            ThresholdAmount = 55000m,
            Currency = "AED"
        };

        // 1. Create
        await repo.CreateRuleAsync(rule);

        // 2. Read by code
        var found = await repo.GetRuleByCodeAsync(TestTenantId, "RULE-AE-LCTR-001");
        Assert.NotNull(found);
        Assert.Equal(55000m, found.ThresholdAmount);

        // 3. Update
        var updated = await repo.UpdateRuleAsync(found with { ScoreWeightage = 45m });
        Assert.Equal(45m, updated.ScoreWeightage);

        // 4. List
        var list = await repo.ListRulesAsync(TestTenantId);
        Assert.Single(list);

        // 5. Delete
        var deleted = await repo.DeleteRuleAsync(TestTenantId, rule.Id);
        Assert.True(deleted);

        var listAfterDelete = await repo.ListRulesAsync(TestTenantId);
        Assert.Empty(listAfterDelete);
    }

    [Fact]
    public void TransactionThreshold_Triggers_When_Amount_Exceeds_Limit()
    {
        var engine = new RuleEngine();

        var rule = new AmlRule
        {
            TenantId = TestTenantId,
            RuleCode = "RULE-AE-LCTR-001",
            RuleName = "Large Cash Transaction",
            Category = RuleCategory.TransactionThreshold,
            RiskLevel = RiskLevel.High,
            ScoreWeightage = 35m,
            ThresholdAmount = 55000m
        };

        var txExceeding = new ScreeningTransaction
        {
            TenantId = TestTenantId,
            CustomerId = Guid.NewGuid(),
            TransactionRefNo = "TX-HIGH-001",
            IdempotencyKey = "IDEMP-1",
            TransactionType = "CASH_DEPOSIT",
            Amount = 75000m,
            Currency = "AED",
            LcyAmount = 75000m,
            SenderName = "Ahmed Al-Mansoor",
            SenderNationality = "AE",
            SenderCountry = "AE",
            BeneficiaryName = "Ahmed Al-Mansoor",
            BeneficiaryCountry = "AE"
        };

        var txUnder = txExceeding with { Amount = 35000m, LcyAmount = 35000m, TransactionRefNo = "TX-LOW-001" };

        var resultsExceeding = engine.EvaluateTransaction(txExceeding, [rule]);
        Assert.Single(resultsExceeding);
        Assert.True(resultsExceeding[0].IsTriggered);
        Assert.Equal(35m, resultsExceeding[0].ScoreContribution);
        Assert.Contains("exceeds threshold limit", resultsExceeding[0].TriggerReason);

        var resultsUnder = engine.EvaluateTransaction(txUnder, [rule]);
        Assert.Single(resultsUnder);
        Assert.False(resultsUnder[0].IsTriggered);
        Assert.Equal(0m, resultsUnder[0].ScoreContribution);
    }

    [Fact]
    public void HighRiskCorridor_Triggers_On_Sanctioned_Jurisdiction()
    {
        var engine = new RuleEngine();

        var rule = new AmlRule
        {
            TenantId = TestTenantId,
            RuleCode = "RULE-FATF-GREY-001",
            RuleName = "FATF High Risk Corridor",
            Category = RuleCategory.HighRiskCorridor,
            RiskLevel = RiskLevel.Critical,
            ScoreWeightage = 50m,
            HighRiskCountries = ["IR", "KP", "MM", "SY"]
        };

        var txHighRisk = new ScreeningTransaction
        {
            TenantId = TestTenantId,
            CustomerId = Guid.NewGuid(),
            TransactionRefNo = "TX-CORRIDOR-001",
            IdempotencyKey = "IDEMP-2",
            TransactionType = "WIRE_TRANSFER",
            Amount = 10000m,
            Currency = "USD",
            LcyAmount = 10000m,
            SenderName = "Global Logistics",
            SenderNationality = "IR",
            SenderCountry = "IR",
            BeneficiaryName = "Dubai Importers",
            BeneficiaryCountry = "AE"
        };

        var txClean = txHighRisk with { SenderCountry = "AE", SenderNationality = "AE", TransactionRefNo = "TX-CORRIDOR-002" };

        var resultsHighRisk = engine.EvaluateTransaction(txHighRisk, [rule]);
        Assert.Single(resultsHighRisk);
        Assert.True(resultsHighRisk[0].IsTriggered);
        Assert.Contains("High-risk jurisdiction detected", resultsHighRisk[0].TriggerReason);

        var resultsClean = engine.EvaluateTransaction(txClean, [rule]);
        Assert.Single(resultsClean);
        Assert.False(resultsClean[0].IsTriggered);
    }

    [Fact]
    public void StructuringSmurfing_Triggers_Within_Sensitive_Window()
    {
        var engine = new RuleEngine();

        var rule = new AmlRule
        {
            TenantId = TestTenantId,
            RuleCode = "RULE-SMURF-001",
            RuleName = "Threshold Avoidance Structuring Window",
            Category = RuleCategory.StructuringSmurfing,
            RiskLevel = RiskLevel.High,
            ScoreWeightage = 45m,
            StructuringMinAmount = 48000m,
            StructuringMaxAmount = 49999m
        };

        var txSmurfing = new ScreeningTransaction
        {
            TenantId = TestTenantId,
            CustomerId = Guid.NewGuid(),
            TransactionRefNo = "TX-SMURF-001",
            IdempotencyKey = "IDEMP-3",
            TransactionType = "CASH_DEPOSIT",
            Amount = 49500m,
            Currency = "AED",
            LcyAmount = 49500m,
            SenderName = "Cash Depositor",
            SenderNationality = "AE",
            SenderCountry = "AE",
            BeneficiaryName = "Trade Account",
            BeneficiaryCountry = "AE"
        };

        var txNormal = txSmurfing with { Amount = 25000m, LcyAmount = 25000m, TransactionRefNo = "TX-NORMAL-001" };

        var resultsSmurfing = engine.EvaluateTransaction(txSmurfing, [rule]);
        Assert.Single(resultsSmurfing);
        Assert.True(resultsSmurfing[0].IsTriggered);
        Assert.Contains("Possible structuring/smurfing pattern", resultsSmurfing[0].TriggerReason);

        var resultsNormal = engine.EvaluateTransaction(txNormal, [rule]);
        Assert.Single(resultsNormal);
        Assert.False(resultsNormal[0].IsTriggered);
    }

    [Fact]
    public void RuleBacktester_Calculates_Simulated_Alert_Rate()
    {
        var ruleEngine = new RuleEngine();
        var backtester = new RuleBacktester(ruleEngine);

        var rule = new AmlRule
        {
            TenantId = TestTenantId,
            RuleCode = "RULE-AE-LCTR-001",
            RuleName = "Large Cash Transaction Reporting >= 55k",
            Category = RuleCategory.TransactionThreshold,
            RiskLevel = RiskLevel.High,
            ScoreWeightage = 30m,
            ThresholdAmount = 55000m
        };

        // Create 100 historical transactions, 5 of which exceed 55,000
        var historical = new List<ScreeningTransaction>();
        for (int i = 1; i <= 100; i++)
        {
            decimal amt = (i % 20 == 0) ? 60000m : 10000m; // 5 hits out of 100 = 5%
            historical.Add(new ScreeningTransaction
            {
                TenantId = TestTenantId,
                CustomerId = Guid.NewGuid(),
                TransactionRefNo = $"HIST-TX-{i:D4}",
                IdempotencyKey = $"HIST-IDEMP-{i}",
                TransactionType = "WIRE",
                Amount = amt,
                Currency = "AED",
                LcyAmount = amt,
                SenderName = "Sender",
                SenderNationality = "AE",
                SenderCountry = "AE",
                BeneficiaryName = "Beneficiary",
                BeneficiaryCountry = "AE"
            });
        }

        var result = backtester.BacktestRule(rule, historical);

        Assert.Equal(100, result.TotalEvaluated);
        Assert.Equal(5, result.TriggeredCount);
        Assert.Equal(5.0, result.HitRatePercentage);
        Assert.Equal(500.0, result.EstimatedDailyAlerts); // 5% of 10,000 avg volume
        Assert.Equal(5, result.SampleTriggeredRefNos.Count);
    }
}
