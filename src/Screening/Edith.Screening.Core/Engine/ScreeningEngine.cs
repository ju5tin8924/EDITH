using System.Diagnostics;
using Edith.Screening.Core.Models;
using Edith.Screening.Core.Normalization;
using Edith.Screening.Core.Scoring;
using Edith.Screening.Core.Search;

namespace Edith.Screening.Core.Engine;

public interface IScreeningEngine
{
    Task<ScreeningDecision> ScreenTransactionAsync(
        ScreeningTransaction transaction,
        ScoreConfig config,
        IReadOnlyList<WhitelistEntry>? activeWhitelists = null,
        CancellationToken ct = default);
}

public sealed class ScreeningEngine : IScreeningEngine
{
    private readonly ITextNormalizer _normalizer;
    private readonly IWatchlistSearchEngine _searchEngine;
    private readonly INameSimilarityScorer _nameScorer;

    public ScreeningEngine(
        ITextNormalizer normalizer,
        IWatchlistSearchEngine searchEngine,
        INameSimilarityScorer nameScorer)
    {
        _normalizer = normalizer;
        _searchEngine = searchEngine;
        _nameScorer = nameScorer;
    }

    public async Task<ScreeningDecision> ScreenTransactionAsync(
        ScreeningTransaction transaction,
        ScoreConfig config,
        IReadOnlyList<WhitelistEntry>? activeWhitelists = null,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var passLogs = new List<ScreeningPassLog>();
        var allCandidateMatches = new List<CandidateMatch>();

        // -------------------------------------------------------------
        // PASS 1: Normalization & Cleansing
        // -------------------------------------------------------------
        var normSender = _normalizer.Normalize(transaction.SenderName);
        var normBeneficiary = _normalizer.Normalize(transaction.BeneficiaryName);
        var normIntermediary = !string.IsNullOrWhiteSpace(transaction.IntermediaryBankName)
            ? _normalizer.Normalize(transaction.IntermediaryBankName, stripCorporateNoise: true)
            : null;

        passLogs.Add(new ScreeningPassLog
        {
            PassNumber = 1,
            Score = 0.00m,
            MatchType = "NORMALIZATION",
            Details = $"Cleaned Sender='{normSender}', Beneficiary='{normBeneficiary}', Bank='{normIntermediary ?? "N/A"}'"
        });

        // -------------------------------------------------------------
        // PASS 2 & 4: Concurrent Multi-Entity Retrieval & Evaluation
        // -------------------------------------------------------------
        var rolesToEvaluate = new List<(EntityRole Role, string NormalizedName, string? Country, string? Nationality)>
        {
            (EntityRole.Sender, normSender, transaction.SenderCountry, transaction.SenderNationality),
            (EntityRole.Beneficiary, normBeneficiary, transaction.BeneficiaryCountry, null)
        };

        if (!string.IsNullOrWhiteSpace(normIntermediary))
        {
            rolesToEvaluate.Add((EntityRole.IntermediaryBank, normIntermediary, null, null));
        }

        // Run screening across all transactional roles in parallel
        var evaluationTasks = rolesToEvaluate.Select(r => EvaluateRoleAsync(r.Role, r.NormalizedName, r.Country, r.Nationality, config, ct));
        var roleResults = await Task.WhenAll(evaluationTasks);

        foreach (var result in roleResults)
        {
            allCandidateMatches.AddRange(result);
        }

        passLogs.Add(new ScreeningPassLog
        {
            PassNumber = 2,
            Score = allCandidateMatches.Count > 0 ? (decimal)allCandidateMatches.Max(m => m.NameSimilarityScore) : 0.00m,
            MatchType = "BROAD_RETRIEVAL",
            Details = $"Dispatched multi-role broad search. Found {allCandidateMatches.Count} candidates across {rolesToEvaluate.Count} roles."
        });

        passLogs.Add(new ScreeningPassLog
        {
            PassNumber = 3,
            Score = allCandidateMatches.Count > 0 ? (decimal)allCandidateMatches.Max(m => m.PhoneticMatchScore * 100) : 0.00m,
            MatchType = "PHONETIC_SCORING",
            Details = "Executed Double Metaphone & string distance analysis on candidate pool."
        });

        passLogs.Add(new ScreeningPassLog
        {
            PassNumber = 4,
            Score = allCandidateMatches.Count > 0 ? allCandidateMatches.Max(m => m.CompositeScore) : 0.00m,
            MatchType = "MULTI_ENTITY_EVALUATION",
            Details = $"Evaluated Sender, Beneficiary, and Correspondent Bank concurrently."
        });

        // -------------------------------------------------------------
        // PASS 5: Deterministic Scoring & Whitelist Bypass
        // -------------------------------------------------------------
        decimal highestScore = 0.00m;
        CandidateMatch? topMatch = null;

        if (allCandidateMatches.Count > 0)
        {
            topMatch = allCandidateMatches.OrderByDescending(m => m.CompositeScore).First();
            highestScore = topMatch.CompositeScore;
        }

        var isOverThreshold = highestScore >= config.MinThresholdScore;
        var isWhitelisted = false;
        string? whitelistReason = null;

        if (isOverThreshold && activeWhitelists != null && allCandidateMatches.Count > 0)
        {
            var matchedWhitelist = activeWhitelists.FirstOrDefault(w =>
                w.IsActive &&
                (w.ExpiresAt == null || w.ExpiresAt > DateTimeOffset.UtcNow) &&
                allCandidateMatches.Any(m => m.CompositeScore >= config.MinThresholdScore && (
                    string.Equals(w.EntityIdentifier, m.Entity.EntityExternalId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(w.EntityIdentifier, m.NormalizedInput, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(_normalizer.Normalize(w.EntityIdentifier), m.NormalizedTarget, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(_normalizer.Normalize(w.EntityIdentifier), m.NormalizedInput, StringComparison.OrdinalIgnoreCase) ||
                    w.CustomerId == transaction.CustomerId)));

            if (matchedWhitelist != null)
            {
                isWhitelisted = true;
                whitelistReason = matchedWhitelist.Reason;
            }
        }

        passLogs.Add(new ScreeningPassLog
        {
            PassNumber = 5,
            Score = highestScore,
            MatchType = isWhitelisted ? "WHITELIST_SUPPRESSION" : (isOverThreshold ? "THRESHOLD_EXCEEDED" : "CLEARED"),
            Details = isWhitelisted
                ? $"Match of {highestScore}% suppressed by active whitelist: '{whitelistReason}'"
                : $"Score {highestScore}% evaluated against threshold {config.MinThresholdScore}%."
        });

        stopwatch.Stop();

        if (isOverThreshold && !isWhitelisted && topMatch != null)
        {
            return new ScreeningDecision
            {
                Status = ScreeningStatus.Held,
                OverallScore = highestScore,
                HoldReason = $"{topMatch.Entity.Category}_MATCH_{highestScore:F1}% on {topMatch.Role} ({topMatch.Entity.FullName})",
                TriggeredList = topMatch.Entity.SourceList,
                CaseNumber = $"CASE-{DateTime.UtcNow:yyyyMMdd}-{Math.Abs(transaction.TransactionRefNo.GetHashCode()):X6}",
                IsWhitelisted = false,
                CandidateMatches = allCandidateMatches.OrderByDescending(m => m.CompositeScore).ToList(),
                PassLogs = passLogs,
                ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds
            };
        }

        return new ScreeningDecision
        {
            Status = ScreeningStatus.Cleared,
            OverallScore = highestScore,
            HoldReason = isWhitelisted ? $"CLEARED_BY_WHITELIST: {whitelistReason}" : null,
            TriggeredList = null,
            CaseNumber = null,
            IsWhitelisted = isWhitelisted,
            CandidateMatches = allCandidateMatches.OrderByDescending(m => m.CompositeScore).ToList(),
            PassLogs = passLogs,
            ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds
        };
    }

    private async Task<List<CandidateMatch>> EvaluateRoleAsync(
        EntityRole role,
        string normalizedInput,
        string? country,
        string? nationality,
        ScoreConfig config,
        CancellationToken ct)
    {
        var matches = new List<CandidateMatch>();
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return matches;
        }

        var candidates = await _searchEngine.SearchCandidatesAsync(normalizedInput, maxResults: 50, ct);

        foreach (var candidate in candidates)
        {
            var normalizedTarget = _normalizer.Normalize(candidate.FullName, candidate.EntityType == "CORPORATE");
            var scoreResult = _nameScorer.Score(normalizedInput, normalizedTarget);

            // Compute composite score using weights
            // Base name score weightage
            var nameContribution = (decimal)scoreResult.FinalScore * (config.NameWeightage / 100m);
            var compositeScore = nameContribution;

            // Nationality weightage
            if (!string.IsNullOrEmpty(nationality) && !string.IsNullOrEmpty(candidate.Nationality))
            {
                if (string.Equals(nationality, candidate.Nationality, StringComparison.OrdinalIgnoreCase))
                {
                    compositeScore += 100m * (config.NationalityWeightage / 100m);
                }
            }
            else
            {
                // Prorate if nationality attribute not present on entity
                compositeScore += (decimal)scoreResult.FinalScore * (config.NationalityWeightage / 100m);
            }

            // Prorate remaining weights (DOB, ID) based on name match if not screened in transaction
            var remainingWeight = config.DobWeightage + config.IdWeightage;
            compositeScore += (decimal)scoreResult.FinalScore * (remainingWeight / 100m);

            compositeScore = Math.Min(100m, Math.Round(compositeScore, 2));
            if (compositeScore < 65m) continue;

            matches.Add(new CandidateMatch
            {
                Entity = candidate,
                Role = role,
                NormalizedInput = normalizedInput,
                NormalizedTarget = normalizedTarget,
                NameSimilarityScore = scoreResult.FinalScore,
                PhoneticMatchScore = scoreResult.PhoneticSimilarity,
                CompositeScore = compositeScore,
                MatchDetails = $"[{role}] {scoreResult.MatchRationate}"
            });
        }

        return matches;
    }
}
