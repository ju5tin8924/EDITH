using Edith.Rules.Engine;
using Edith.Rules.Models;
using Edith.Screening.Core.Models;

namespace Edith.Rules.Sandbox;

public interface IRuleBacktester
{
    BacktestResult BacktestRule(
        AmlRule candidateRule,
        IReadOnlyList<ScreeningTransaction> historicalTransactions,
        AmlRule? baselineRule = null,
        IReadOnlyList<AmlRule>? activePortfolioRules = null);
}

public sealed class RuleBacktester : IRuleBacktester
{
    private readonly IRuleEngine _ruleEngine;

    public RuleBacktester(IRuleEngine ruleEngine)
    {
        _ruleEngine = ruleEngine;
    }

    public BacktestResult BacktestRule(
        AmlRule candidateRule,
        IReadOnlyList<ScreeningTransaction> historicalTransactions,
        AmlRule? baselineRule = null,
        IReadOnlyList<AmlRule>? activePortfolioRules = null)
    {
        var candidateHits = EvaluateDataset(candidateRule, historicalTransactions);
        var baselineHits = baselineRule != null
            ? EvaluateDataset(baselineRule, historicalTransactions)
            : candidateHits;

        var otherRules = (activePortfolioRules ?? [])
            .Where(r => r.IsActive && !string.Equals(r.RuleCode, candidateRule.RuleCode, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var triggeredRefNos = candidateHits.Keys.ToList();
        var sampleHits = new List<BacktestSampleHit>();

        int newlyCaptured = 0;
        int newlySuppressed = 0;
        int uniqueHits = 0;
        int overlappingHits = 0;
        int tpCaptured = 0;
        int totalStrs = 0;
        int fpFlagged = 0;

        foreach (var tx in historicalTransactions)
        {
            var groundTruth = ClassifyGroundTruth(tx);
            if (groundTruth == "STR_FILED")
            {
                totalStrs++;
            }

            var inCandidate = candidateHits.TryGetValue(tx.TransactionRefNo, out var candidateReason);
            var inBaseline = baselineHits.TryGetValue(tx.TransactionRefNo, out var baselineReason);

            if (inCandidate && !inBaseline) newlyCaptured++;
            if (!inCandidate && inBaseline) newlySuppressed++;

            if (inCandidate)
            {
                if (groundTruth == "STR_FILED") tpCaptured++;
                else if (groundTruth == "CLEARED_FP") fpFlagged++;

                var overlaps = otherRules.Count > 0
                    ? _ruleEngine.EvaluateTransaction(tx, otherRules)
                        .Where(r => r.IsTriggered)
                        .Select(r => r.RuleCode)
                        .ToList()
                    : [];

                if (overlaps.Count > 0) overlappingHits++;
                else uniqueHits++;

                if (sampleHits.Count < 16)
                {
                    sampleHits.Add(new BacktestSampleHit
                    {
                        TransactionRefNo = tx.TransactionRefNo,
                        SenderName = tx.SenderName,
                        BeneficiaryName = tx.BeneficiaryName,
                        Amount = tx.Amount,
                        Currency = tx.Currency,
                        Corridor = $"{tx.SenderCountry} -> {tx.BeneficiaryCountry}",
                        TriggerReason = candidateReason ?? "Matched rule criteria",
                        DeltaStatus = !inBaseline ? "NEWLY_CAPTURED" : "RETAINED",
                        GroundTruthLabel = groundTruth,
                        OverlappingRules = overlaps
                    });
                }
            }
            else if (inBaseline && sampleHits.Count < 20)
            {
                sampleHits.Add(new BacktestSampleHit
                {
                    TransactionRefNo = tx.TransactionRefNo,
                    SenderName = tx.SenderName,
                    BeneficiaryName = tx.BeneficiaryName,
                    Amount = tx.Amount,
                    Currency = tx.Currency,
                    Corridor = $"{tx.SenderCountry} -> {tx.BeneficiaryCountry}",
                    TriggerReason = $"Suppressed (Previously triggered: {baselineReason})",
                    DeltaStatus = "NEWLY_SUPPRESSED",
                    GroundTruthLabel = groundTruth,
                    OverlappingRules = []
                });
            }
        }

        var total = historicalTransactions.Count;
        var hitCount = triggeredRefNos.Count;
        var hitRate = total > 0 ? (double)hitCount / total * 100.0 : 0.0;
        var estimatedDailyAlerts = Math.Round((hitRate / 100.0) * 10000.0, 1);
        var empiricalFpRate = hitCount > 0
            ? Math.Round((double)fpFlagged / hitCount * 100.0, 1)
            : 0.0;

        var sensitivityCurve = BuildSensitivityCurve(candidateRule, historicalTransactions);

        return new BacktestResult
        {
            RuleId = candidateRule.Id,
            RuleCode = candidateRule.RuleCode,
            TotalEvaluated = total,
            TriggeredCount = hitCount,
            HitRatePercentage = Math.Round(hitRate, 2),
            EstimatedDailyAlerts = estimatedDailyAlerts,
            SampleTriggeredRefNos = triggeredRefNos.Take(25).ToList(),
            SampleHits = sampleHits,
            BaselineTriggeredCount = baselineHits.Count,
            NetAlertDelta = hitCount - baselineHits.Count,
            NewlyCapturedCount = newlyCaptured,
            NewlySuppressedCount = newlySuppressed,
            UniqueHitsCount = uniqueHits,
            OverlappingHitsCount = overlappingHits,
            TruePositivesCaptured = tpCaptured,
            TotalHistoricalStrs = totalStrs,
            FalsePositivesFlagged = fpFlagged,
            EmpiricalFalsePositiveRate = empiricalFpRate,
            SensitivityCurve = sensitivityCurve
        };
    }

    private Dictionary<string, string> EvaluateDataset(AmlRule rule, IReadOnlyList<ScreeningTransaction> transactions)
    {
        var activeCandidate = rule with { IsActive = true };
        var hits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var senderWindows = new Dictionary<string, Queue<DateTimeOffset>>(StringComparer.OrdinalIgnoreCase);
        var structuringWindows = new Dictionary<string, Queue<(DateTimeOffset Date, decimal Amount)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var tx in transactions)
        {
            var results = _ruleEngine.EvaluateTransaction(tx, [activeCandidate]);
            var triggeredResult = results.FirstOrDefault(r => r.IsTriggered);
            var isHit = triggeredResult != null;
            var hitReason = triggeredResult?.TriggerReason;

            if (!isHit && activeCandidate.Category == RuleCategory.VelocityAnomaly &&
                activeCandidate.WindowMinutes is > 0 && activeCandidate.MinTransactions is > 1)
            {
                if (!senderWindows.TryGetValue(tx.SenderName, out var windowQueue))
                {
                    windowQueue = new Queue<DateTimeOffset>();
                    senderWindows[tx.SenderName] = windowQueue;
                }

                var windowSpan = TimeSpan.FromMinutes(activeCandidate.WindowMinutes.Value);
                while (windowQueue.Count > 0 && Math.Abs((tx.TransactionDate - windowQueue.Peek()).TotalMinutes) > windowSpan.TotalMinutes)
                {
                    windowQueue.Dequeue();
                }

                windowQueue.Enqueue(tx.TransactionDate);
                var minAmt = activeCandidate.ThresholdAmount ?? 0m;
                if (windowQueue.Count >= activeCandidate.MinTransactions.Value && tx.LcyAmount >= minAmt)
                {
                    isHit = true;
                    hitReason = $"Velocity window breached: {windowQueue.Count} transfers by '{tx.SenderName}' within {activeCandidate.WindowMinutes}m (>= {activeCandidate.MinTransactions} tx limit)";
                }
            }

            if (activeCandidate.Category == RuleCategory.StructuringSmurfing &&
                activeCandidate.StructuringMinAmount.HasValue && activeCandidate.StructuringMaxAmount.HasValue &&
                activeCandidate.WindowMinutes is > 0 && activeCandidate.MinTransactions is > 1)
            {
                isHit = false;
                hitReason = null;
                var inBand = tx.Amount >= activeCandidate.StructuringMinAmount.Value &&
                             tx.Amount <= activeCandidate.StructuringMaxAmount.Value;
                if (inBand)
                {
                    if (!structuringWindows.TryGetValue(tx.SenderName, out var sQueue))
                    {
                        sQueue = new Queue<(DateTimeOffset, decimal)>();
                        structuringWindows[tx.SenderName] = sQueue;
                    }

                    while (sQueue.Count > 0 && Math.Abs((tx.TransactionDate - sQueue.Peek().Date).TotalMinutes) > activeCandidate.WindowMinutes.Value)
                    {
                        sQueue.Dequeue();
                    }

                    sQueue.Enqueue((tx.TransactionDate, tx.Amount));
                    if (sQueue.Count >= activeCandidate.MinTransactions.Value)
                    {
                        var totalSplit = sQueue.Sum(x => x.Amount);
                        isHit = true;
                        hitReason = $"Split structuring burst: {sQueue.Count} deposits totaling {totalSplit:N0} {tx.Currency} within {activeCandidate.WindowMinutes}m";
                    }
                }
            }

            if (isHit)
            {
                hits[tx.TransactionRefNo] = hitReason ?? "Matched rule criteria";
            }
        }

        return hits;
    }

    private List<SensitivityPoint> BuildSensitivityCurve(AmlRule candidate, IReadOnlyList<ScreeningTransaction> transactions)
    {
        var points = new List<SensitivityPoint>(5);
        decimal[] multipliers = [0.6m, 0.8m, 1.0m, 1.2m, 1.4m];

        foreach (var mult in multipliers)
        {
            AmlRule stepped;
            decimal paramVal;
            string label;

            switch (candidate.Category)
            {
                case RuleCategory.TransactionThreshold:
                case RuleCategory.VelocityAnomaly:
                    var baseAmt = candidate.ThresholdAmount ?? 55000m;
                    paramVal = Math.Round(baseAmt * mult / 1000m) * 1000m;
                    if (paramVal <= 0) paramVal = 5000m;
                    stepped = candidate with { ThresholdAmount = paramVal };
                    label = $"{paramVal / 1000m:N0}k {candidate.Currency ?? "AED"}";
                    break;

                case RuleCategory.StructuringSmurfing:
                    var baseMin = candidate.StructuringMinAmount ?? 45000m;
                    var bandWidth = Math.Max(2000m, (candidate.StructuringMaxAmount ?? 54999m) - baseMin);
                    paramVal = Math.Round(baseMin * mult / 1000m) * 1000m;
                    stepped = candidate with
                    {
                        StructuringMinAmount = paramVal,
                        StructuringMaxAmount = paramVal + bandWidth
                    };
                    label = $"{paramVal / 1000m:N0}k–{(paramVal + bandWidth) / 1000m:N0}k";
                    break;

                default:
                    // HighRiskCorridor: vary country list coverage (top N countries)
                    var countries = candidate.HighRiskCountries ?? ["IR", "KP", "MM", "SY", "RU", "YE"];
                    int count = Math.Clamp((int)Math.Round(countries.Count * (double)mult), 1, Math.Max(1, countries.Count + 2));
                    var pool = countries.Concat(["YE", "AF", "VE", "BY"]).Distinct(StringComparer.OrdinalIgnoreCase).Take(count).ToList();
                    paramVal = pool.Count;
                    stepped = candidate with { HighRiskCountries = pool };
                    label = $"{pool.Count} ISO Corridors";
                    break;
            }

            var steppedHits = EvaluateDataset(stepped, transactions);
            int tp = transactions.Count(t => steppedHits.ContainsKey(t.TransactionRefNo) && ClassifyGroundTruth(t) == "STR_FILED");
            double rate = transactions.Count > 0 ? Math.Round((double)steppedHits.Count / transactions.Count * 100.0, 2) : 0.0;

            points.Add(new SensitivityPoint
            {
                Label = label,
                ParameterValue = paramVal,
                TriggeredCount = steppedHits.Count,
                HitRatePercentage = rate,
                TruePositivesCaptured = tp,
                IsCurrentCandidate = mult == 1.0m
            });
        }

        return points;
    }

    public static string ClassifyGroundTruth(ScreeningTransaction tx)
    {
        bool highRiskCorridor = tx.SenderCountry is "IR" or "KP" or "SY" or "MM" ||
                                tx.BeneficiaryCountry is "IR" or "KP" or "SY" or "MM";
        bool smurfBand = tx.LcyAmount >= 46000m && tx.LcyAmount <= 54900m;
        bool extremeValue = tx.LcyAmount >= 380000m;

        if ((highRiskCorridor && tx.LcyAmount >= 85000m) || (smurfBand && highRiskCorridor) || extremeValue)
        {
            return "STR_FILED";
        }

        if (tx.LcyAmount < 120000m && !highRiskCorridor)
        {
            return "CLEARED_FP";
        }

        return "UNREVIEWED";
    }
}
