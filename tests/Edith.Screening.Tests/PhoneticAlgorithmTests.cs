using Edith.Screening.Core.Scoring;

namespace Edith.Screening.Tests;

public class PhoneticAlgorithmTests
{
    [Fact]
    public void JaroWinkler_returns_perfect_score_for_identical_strings()
    {
        var score = JaroWinkler.CalculateSimilarity("OSAMA BIN LADEN", "OSAMA BIN LADEN");
        Assert.Equal(1.0, score);
    }

    [Fact]
    public void JaroWinkler_boosts_matching_prefixes()
    {
        var simWithPrefix = JaroWinkler.CalculateSimilarity("MOHAMMAD", "MOHAMED");
        var simDifferentPrefix = JaroWinkler.CalculateSimilarity("AHMED", "MEHMET");

        Assert.True(simWithPrefix > 0.85);
        Assert.True(simWithPrefix > simDifferentPrefix);
    }

    [Fact]
    public void DamerauLevenshtein_handles_transpositions()
    {
        // One character transposition: 'IE' vs 'EI'
        var dist = DamerauLevenshtein.CalculateDistance("VLADIMIR", "VLADMIIR");
        var sim = DamerauLevenshtein.CalculateSimilarity("VLADIMIR", "VLADMIIR");

        Assert.Equal(1, dist);
        Assert.True(sim >= 0.875);
    }

    [Theory]
    [InlineData("CATHERINE", "KATHERINE")]
    [InlineData("MOHAMMED", "MUHAMMAD")]
    [InlineData("SMITH", "SMYTHE")]
    [InlineData("XAVIER", "SAVIER")]
    public void DoubleMetaphone_encodes_phonetically_equivalent_names(string name1, string name2)
    {
        var score = DoubleMetaphone.Compare(name1, name2);
        Assert.True(score >= 0.85, $"Expected phonetic similarity >= 0.85 for '{name1}' and '{name2}', got {score}");
    }

    [Fact]
    public void NameSimilarityScorer_scores_inverted_token_names_highly()
    {
        var scorer = new NameSimilarityScorer();
        var result = scorer.Score("SADDAM HUSSEIN", "HUSSEIN, SADDAM");

        Assert.True(result.FinalScore >= 95.0, $"Expected score >= 95.0, got {result.FinalScore}");
    }

    [Fact]
    public void NameSimilarityScorer_detects_fuzzy_typo_variations()
    {
        var scorer = new NameSimilarityScorer();
        var result = scorer.Score("MOHAMMED AL-HASHIMI", "MOHAMED AL HASHMI");

        Assert.True(result.FinalScore >= 80.0, $"Expected score >= 80.0, got {result.FinalScore}");
    }
}
