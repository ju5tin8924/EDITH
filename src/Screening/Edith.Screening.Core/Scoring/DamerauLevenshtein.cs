namespace Edith.Screening.Core.Scoring;

public static class DamerauLevenshtein
{
    public static double CalculateSimilarity(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2))
        {
            return 1.0;
        }

        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
        {
            return 0.0;
        }

        if (string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase))
        {
            return 1.0;
        }

        var distance = CalculateDistance(s1, s2);
        var maxLen = Math.Max(s1.Length, s2.Length);

        return Math.Max(0.0, 1.0 - ((double)distance / maxLen));
    }

    public static int CalculateDistance(string s1, string s2)
    {
        var len1 = s1.Length;
        var len2 = s2.Length;

        var d = new int[len1 + 1, len2 + 1];

        for (var i = 0; i <= len1; i++)
        {
            d[i, 0] = i;
        }

        for (var j = 0; j <= len2; j++)
        {
            d[0, j] = j;
        }

        for (var i = 1; i <= len1; i++)
        {
            for (var j = 1; j <= len2; j++)
            {
                var cost = char.ToUpperInvariant(s1[i - 1]) == char.ToUpperInvariant(s2[j - 1]) ? 0 : 1;

                var deletion = d[i - 1, j] + 1;
                var insertion = d[i, j - 1] + 1;
                var substitution = d[i - 1, j - 1] + cost;

                var min = Math.Min(Math.Min(deletion, insertion), substitution);

                // Check transposition
                if (i > 1 && j > 1 &&
                    char.ToUpperInvariant(s1[i - 1]) == char.ToUpperInvariant(s2[j - 2]) &&
                    char.ToUpperInvariant(s1[i - 2]) == char.ToUpperInvariant(s2[j - 1]))
                {
                    min = Math.Min(min, d[i - 2, j - 2] + cost);
                }

                d[i, j] = min;
            }
        }

        return d[len1, len2];
    }
}
