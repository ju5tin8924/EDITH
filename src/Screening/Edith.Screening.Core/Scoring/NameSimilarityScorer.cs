namespace Edith.Screening.Core.Scoring;

public interface INameSimilarityScorer
{
    NameScoreResult Score(string inputName, string candidateName);
}

public sealed record NameScoreResult
{
    public required double StringSimilarity { get; init; }  // [0.0 - 1.0]
    public required double PhoneticSimilarity { get; init; } // [0.0 - 1.0]
    public required double FinalScore { get; init; }        // [0.0 - 100.0]
    public required string MatchRationate { get; init; }
}

public sealed class NameSimilarityScorer : INameSimilarityScorer
{
    public NameScoreResult Score(string inputName, string candidateName)
    {
        if (string.IsNullOrWhiteSpace(inputName) || string.IsNullOrWhiteSpace(candidateName))
        {
            return new NameScoreResult
            {
                StringSimilarity = 0.0,
                PhoneticSimilarity = 0.0,
                FinalScore = 0.0,
                MatchRationate = "Empty name comparison"
            };
        }

        var s1 = CleanName(inputName);
        var s2 = CleanName(candidateName);

        // 1. Exact string match
        if (string.Equals(s1, s2, StringComparison.Ordinal))
        {
            return new NameScoreResult
            {
                StringSimilarity = 1.0,
                PhoneticSimilarity = 1.0,
                FinalScore = 100.0,
                MatchRationate = "Exact string match (100%)"
            };
        }

        // 2. Token-set analysis for out-of-order names (e.g. "SADDAM HUSSEIN" vs "HUSSEIN SADDAM")
        var tokens1 = s1.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var tokens2 = s2.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var sorted1 = string.Join(" ", tokens1.OrderBy(t => t, StringComparer.Ordinal));
        var sorted2 = string.Join(" ", tokens2.OrderBy(t => t, StringComparer.Ordinal));

        if (string.Equals(sorted1, sorted2, StringComparison.Ordinal))
        {
            return new NameScoreResult
            {
                StringSimilarity = 1.0,
                PhoneticSimilarity = 1.0,
                FinalScore = 98.0,
                MatchRationate = "Token permutation exact match (98%)"
            };
        }

        // 3. String distance metrics
        var jaroWinkler = JaroWinkler.CalculateSimilarity(s1, s2);
        var damerauLevenshtein = DamerauLevenshtein.CalculateSimilarity(s1, s2);
        var stringSim = Math.Max(jaroWinkler, (jaroWinkler * 0.6) + (damerauLevenshtein * 0.4));

        // Token-level phonetic evaluation
        var phoneticSim = EvaluateTokenPhonetics(tokens1, tokens2);

        // If all query tokens match (e.g. middle name omitted in customer records), boost string similarity
        var minTokens = Math.Min(tokens1.Length, tokens2.Length);
        if (minTokens >= 2 && phoneticSim >= 0.85)
        {
            stringSim = Math.Max(stringSim, phoneticSim * 0.90);
        }

        // Blended score calculation:
        // High string similarity with phonetic alignment gets strong boost
        double composite;
        if (phoneticSim >= 0.85 && stringSim >= 0.70)
        {
            composite = (stringSim * 0.50) + (phoneticSim * 0.50);
        }
        else
        {
            composite = (stringSim * 0.70) + (phoneticSim * 0.30);
        }

        var finalScore = Math.Round(composite * 100.0, 2);

        var rationale = $"JW={jaroWinkler:F2}, DL={damerauLevenshtein:F2}, Phonetic={phoneticSim:F2} -> {finalScore}%";

        return new NameScoreResult
        {
            StringSimilarity = stringSim,
            PhoneticSimilarity = phoneticSim,
            FinalScore = finalScore,
            MatchRationate = rationale
        };
    }

    private static double EvaluateTokenPhonetics(string[] tokens1, string[] tokens2)
    {
        if (tokens1.Length == 0 || tokens2.Length == 0)
        {
            return 0.0;
        }

        var totalScore = 0.0;

        foreach (var t1 in tokens1)
        {
            var bestTokenMatch = 0.0;
            foreach (var t2 in tokens2)
            {
                var match = DoubleMetaphone.Compare(t1, t2);
                if (match > bestTokenMatch)
                {
                    bestTokenMatch = match;
                }
            }

            if (bestTokenMatch > 0.0)
            {
                totalScore += bestTokenMatch;
            }
        }

        var minTokens = Math.Min(tokens1.Length, tokens2.Length);
        var maxTokens = Math.Max(tokens1.Length, tokens2.Length);

        var recall = totalScore / minTokens;
        var precision = totalScore / maxTokens;

        if (recall >= 0.95 && minTokens >= 2)
        {
            return (recall * 0.70) + (precision * 0.30);
        }

        return precision;
    }

    private static string CleanName(string input)
    {
        var sb = new System.Text.StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToUpperInvariant(c));
            }
            else
            {
                sb.Append(' ');
            }
        }
        return string.Join(" ", sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
