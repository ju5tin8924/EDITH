using Edith.Rules.Engine;
using Edith.Rules.Models;
using Edith.Screening.Core.Models;

namespace Edith.Rules.Sandbox;

public interface IRuleBacktester
{
    BacktestResult BacktestRule(AmlRule candidateRule, IReadOnlyList<ScreeningTransaction> historicalTransactions);
}

public sealed class RuleBacktester : IRuleBacktester
{
    private readonly IRuleEngine _ruleEngine;

    public RuleBacktester(IRuleEngine ruleEngine)
    {
        _ruleEngine = ruleEngine;
    }

    public BacktestResult BacktestRule(AmlRule candidateRule, IReadOnlyList<ScreeningTransaction> historicalTransactions)
    {
        var triggeredRefNos = new List<string>();

        foreach (var tx in historicalTransactions)
        {
            var results = _ruleEngine.EvaluateTransaction(tx, [candidateRule]);
            if (results.Any(r => r.IsTriggered))
            {
                triggeredRefNos.Add(tx.TransactionRefNo);
            }
        }

        var total = historicalTransactions.Count;
        var hitCount = triggeredRefNos.Count;
        var hitRate = total > 0 ? (double)hitCount / total * 100.0 : 0.0;

        // Model estimated daily alerts based on 10,000 average daily volume
        var estimatedDailyAlerts = Math.Round((hitRate / 100.0) * 10000.0, 1);

        return new BacktestResult
        {
            RuleId = candidateRule.Id,
            RuleCode = candidateRule.RuleCode,
            TotalEvaluated = total,
            TriggeredCount = hitCount,
            HitRatePercentage = Math.Round(hitRate, 2),
            EstimatedDailyAlerts = estimatedDailyAlerts,
            SampleTriggeredRefNos = triggeredRefNos.Take(25).ToList()
        };
    }
}
