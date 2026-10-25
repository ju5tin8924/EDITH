using Edith.Rules.Models;
using Edith.Screening.Core.Models;

namespace Edith.Rules.Engine;

public interface IRuleEngine
{
    IReadOnlyList<RuleEvaluationResult> EvaluateTransaction(ScreeningTransaction transaction, IReadOnlyList<AmlRule> rules);
}

public sealed class RuleEngine : IRuleEngine
{
    public IReadOnlyList<RuleEvaluationResult> EvaluateTransaction(ScreeningTransaction transaction, IReadOnlyList<AmlRule> rules)
    {
        var results = new List<RuleEvaluationResult>();

        foreach (var rule in rules.Where(r => r.IsActive))
        {
            var (triggered, reason) = EvaluateSingleRule(transaction, rule);
            results.Add(new RuleEvaluationResult
            {
                RuleId = rule.Id,
                RuleCode = rule.RuleCode,
                RuleName = rule.RuleName,
                IsTriggered = triggered,
                ScoreContribution = triggered ? rule.ScoreWeightage : 0.0m,
                TriggerReason = reason
            });
        }

        return results;
    }

    private static (bool Triggered, string? Reason) EvaluateSingleRule(ScreeningTransaction tx, AmlRule rule)
    {
        switch (rule.Category)
        {
            case RuleCategory.TransactionThreshold:
                if (rule.ThresholdAmount.HasValue && tx.LcyAmount >= rule.ThresholdAmount.Value)
                {
                    return (true, $"Transaction amount {tx.LcyAmount:N2} LCY exceeds threshold limit {rule.ThresholdAmount.Value:N2}");
                }
                return (false, null);

            case RuleCategory.HighRiskCorridor:
                if (rule.HighRiskCountries != null && rule.HighRiskCountries.Count > 0)
                {
                    var highRiskSet = new HashSet<string>(rule.HighRiskCountries, StringComparer.OrdinalIgnoreCase);
                    var senderHit = !string.IsNullOrEmpty(tx.SenderCountry) && highRiskSet.Contains(tx.SenderCountry);
                    var beneHit = !string.IsNullOrEmpty(tx.BeneficiaryCountry) && highRiskSet.Contains(tx.BeneficiaryCountry);

                    if (senderHit || beneHit)
                    {
                        var country = senderHit ? tx.SenderCountry : tx.BeneficiaryCountry;
                        return (true, $"High-risk jurisdiction detected in transaction corridor: '{country}'");
                    }
                }
                return (false, null);

            case RuleCategory.StructuringSmurfing:
                if (rule.StructuringMinAmount.HasValue && rule.StructuringMaxAmount.HasValue)
                {
                    if (tx.Amount >= rule.StructuringMinAmount.Value && tx.Amount <= rule.StructuringMaxAmount.Value)
                    {
                        return (true, $"Possible structuring/smurfing pattern: Amount {tx.Amount:N2} falls within sensitive threshold window [{rule.StructuringMinAmount.Value:N2} - {rule.StructuringMaxAmount.Value:N2}]");
                    }
                }
                return (false, null);

            case RuleCategory.VelocityAnomaly:
                var minTx = rule.MinTransactions ?? 3;
                var winMins = rule.WindowMinutes ?? 60;
                var velocityFloor = rule.ThresholdAmount is > 0m ? rule.ThresholdAmount.Value : 200000m;
                if (tx.LcyAmount >= velocityFloor)
                {
                    return (true, $"Velocity burst anomaly: Transfer of {tx.LcyAmount:N2} LCY breaches velocity floor {velocityFloor:N2} ({minTx}+ tx / {winMins}m profile)");
                }
                return (false, null);

            default:
                return (false, null);
        }
    }
}
