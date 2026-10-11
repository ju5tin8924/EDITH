namespace Edith.Screening.Core.Scoring;

public static class JaroWinkler
{
    private const double DefaultThreshold = 0.7;
    private const double DefaultPrefixScale = 0.1;
    private const int MaxPrefixLength = 4;

    public static double CalculateSimilarity(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
        {
            return 0.0;
        }

        if (string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase))
        {
            return 1.0;
        }

        var jaroDistance = CalculateJaro(s1, s2);
        if (jaroDistance < DefaultThreshold)
        {
            return jaroDistance;
        }

        // Winkler prefix bonus
        var prefixLen = 0;
        var maxPrefix = Math.Min(Math.Min(s1.Length, s2.Length), MaxPrefixLength);

        for (var i = 0; i < maxPrefix; i++)
        {
            if (char.ToUpperInvariant(s1[i]) == char.ToUpperInvariant(s2[i]))
            {
                prefixLen++;
            }
            else
            {
                break;
            }
        }

        return jaroDistance + (prefixLen * DefaultPrefixScale * (1.0 - jaroDistance));
    }

    private static double CalculateJaro(string s1, string s2)
    {
        var len1 = s1.Length;
        var len2 = s2.Length;

        var matchWindow = Math.Max(len1, len2) / 2 - 1;
        if (matchWindow < 0)
        {
            matchWindow = 0;
        }

        var s1Matches = new bool[len1];
        var s2Matches = new bool[len2];

        var matches = 0;
        for (var i = 0; i < len1; i++)
        {
            var start = Math.Max(0, i - matchWindow);
            var end = Math.Min(i + matchWindow + 1, len2);

            for (var j = start; j < end; j++)
            {
                if (s2Matches[j] || char.ToUpperInvariant(s1[i]) != char.ToUpperInvariant(s2[j]))
                {
                    continue;
                }

                s1Matches[i] = true;
                s2Matches[j] = true;
                matches++;
                break;
            }
        }

        if (matches == 0)
        {
            return 0.0;
        }

        var transpositions = 0;
        var k = 0;
        for (var i = 0; i < len1; i++)
        {
            if (!s1Matches[i])
            {
                continue;
            }

            while (!s2Matches[k])
            {
                k++;
            }

            if (char.ToUpperInvariant(s1[i]) != char.ToUpperInvariant(s2[k]))
            {
                transpositions++;
            }

            k++;
        }

        var t = transpositions / 2.0;

        return ((matches / (double)len1) + (matches / (double)len2) + ((matches - t) / matches)) / 3.0;
    }
}
