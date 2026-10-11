using System.Globalization;
using System.Text;

namespace Edith.Screening.Core.Normalization;

public interface ITextNormalizer
{
    string Normalize(string? input, bool stripCorporateNoise = false);
}

public sealed class TextNormalizer : ITextNormalizer
{
    private static readonly HashSet<string> CorporateStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "LLC", "LTD", "LIMITED", "FZE", "FZ-LLC", "FZLLC",
        "CORP", "CORPORATION", "INC", "INCORPORATED",
        "COMPANY", "CO", "GMBH", "SA", "PLC", "PJSC",
        "TRADING", "HOLDINGS", "GROUP", "ENTERPRISES",
        "BANK", "GENERAL", "INTERNATIONAL", "GLOBAL"
    };

    public string Normalize(string? input, bool stripCorporateNoise = false)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        // 1. ASCII folding (strip diacritics via FormD decomposition)
        var normalizedFormD = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalizedFormD.Length);

        foreach (var c in normalizedFormD)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
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
        }

        var cleaned = sb.ToString();

        // 2. Tokenize and clean up noise words if corporate flag requested
        var tokens = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return string.Empty;
        }

        if (stripCorporateNoise)
        {
            var filteredTokens = new List<string>(tokens.Length);
            foreach (var token in tokens)
            {
                if (!CorporateStopWords.Contains(token))
                {
                    filteredTokens.Add(token);
                }
            }

            // Fallback to original tokens if all tokens were corporate words (e.g., "BANK OF ...")
            if (filteredTokens.Count > 0)
            {
                return string.Join(" ", filteredTokens);
            }
        }

        return string.Join(" ", tokens);
    }
}
