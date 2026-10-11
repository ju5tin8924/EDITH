using System.Text;

namespace Edith.Screening.Core.Scoring;

public sealed record MetaphoneResult(string Primary, string Alternate);

public static class DoubleMetaphone
{
    private const int MaxKeyLength = 4;

    public static MetaphoneResult Encode(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new MetaphoneResult(string.Empty, string.Empty);
        }

        var word = CleanInput(input);
        if (word.Length == 0)
        {
            return new MetaphoneResult(string.Empty, string.Empty);
        }

        var primary = new StringBuilder();
        var alternate = new StringBuilder();

        var current = 0;
        var length = word.Length;

        // Skip initial silent letters
        if (StringAt(word, 0, 2, "GN", "KN", "PN", "WR", "PS"))
        {
            current++;
        }

        // Initial 'X' sounds like 'S'
        if (word[0] == 'X')
        {
            primary.Append('S');
            alternate.Append('S');
            current++;
        }

        while (primary.Length < MaxKeyLength || alternate.Length < MaxKeyLength)
        {
            if (current >= length)
            {
                break;
            }

            switch (word[current])
            {
                case 'A':
                case 'E':
                case 'I':
                case 'O':
                case 'U':
                case 'Y':
                    if (current == 0)
                    {
                        primary.Append('A');
                        alternate.Append('A');
                    }
                    current++;
                    break;

                case 'B':
                    primary.Append('P');
                    alternate.Append('P');
                    current = (current + 1 < length && word[current + 1] == 'B') ? current + 2 : current + 1;
                    break;

                case 'C':
                    if (current > 1 && !IsVowel(word, current - 2) && StringAt(word, current - 1, 3, "ACH") &&
                        (current + 2 >= length || word[current + 2] != 'I' && word[current + 2] != 'E') ||
                        StringAt(word, current - 2, 6, "BACHER", "MACHER"))
                    {
                        primary.Append('K');
                        alternate.Append('K');
                        current += 2;
                        break;
                    }

                    if (current == 0 && StringAt(word, current, 6, "CAESAR"))
                    {
                        primary.Append('S');
                        alternate.Append('S');
                        current += 2;
                        break;
                    }

                    if (StringAt(word, current, 2, "CH"))
                    {
                        primary.Append('X');
                        alternate.Append('K');
                        current += 2;
                        break;
                    }

                    if (StringAt(word, current, 2, "CZ"))
                    {
                        primary.Append('S');
                        alternate.Append('X');
                        current += 2;
                        break;
                    }

                    if (StringAt(word, current, 2, "CI", "CE", "CY"))
                    {
                        primary.Append('S');
                        alternate.Append('S');
                        current += 2;
                        break;
                    }

                    primary.Append('K');
                    alternate.Append('K');
                    if (StringAt(word, current + 1, 2, " C", " Q", " G"))
                    {
                        current += 3;
                    }
                    else if (StringAt(word, current + 1, 1, "C", "K", "Q") && !StringAt(word, current + 1, 2, "CE", "CI"))
                    {
                        current += 2;
                    }
                    else
                    {
                        current++;
                    }
                    break;

                case 'D':
                    if (StringAt(word, current, 2, "DG"))
                    {
                        if (StringAt(word, current + 2, 1, "I", "E", "Y"))
                        {
                            primary.Append('J');
                            alternate.Append('J');
                            current += 3;
                        }
                        else
                        {
                            primary.Append("TK");
                            alternate.Append("TK");
                            current += 2;
                        }
                    }
                    else if (StringAt(word, current, 2, "DT", "DD"))
                    {
                        primary.Append('T');
                        alternate.Append('T');
                        current += 2;
                    }
                    else
                    {
                        primary.Append('T');
                        alternate.Append('T');
                        current++;
                    }
                    break;

                case 'F':
                    primary.Append('F');
                    alternate.Append('F');
                    current = (current + 1 < length && word[current + 1] == 'F') ? current + 2 : current + 1;
                    break;

                case 'G':
                    if (current + 1 < length && word[current + 1] == 'H')
                    {
                        if (current > 0 && !IsVowel(word, current - 1))
                        {
                            primary.Append('K');
                            alternate.Append('K');
                            current += 2;
                        }
                        else if (current == 0)
                        {
                            primary.Append('K');
                            alternate.Append('K');
                            current += 2;
                        }
                        else
                        {
                            current += 2;
                        }
                    }
                    else if (current + 1 < length && word[current + 1] == 'N')
                    {
                        primary.Append('K');
                        alternate.Append('N');
                        current += 2;
                    }
                    else if (StringAt(word, current, 2, "GI", "GE", "GY"))
                    {
                        primary.Append('K');
                        alternate.Append('J');
                        current += 2;
                    }
                    else
                    {
                        primary.Append('K');
                        alternate.Append('K');
                        current = (current + 1 < length && word[current + 1] == 'G') ? current + 2 : current + 1;
                    }
                    break;

                case 'H':
                    if ((current == 0 || IsVowel(word, current - 1)) && IsVowel(word, current + 1))
                    {
                        primary.Append('H');
                        alternate.Append('H');
                        current += 2;
                    }
                    else
                    {
                        current++;
                    }
                    break;

                case 'J':
                    primary.Append('J');
                    alternate.Append('A');
                    current = (current + 1 < length && word[current + 1] == 'J') ? current + 2 : current + 1;
                    break;

                case 'K':
                    primary.Append('K');
                    alternate.Append('K');
                    current = (current + 1 < length && word[current + 1] == 'K') ? current + 2 : current + 1;
                    break;

                case 'L':
                    primary.Append('L');
                    alternate.Append('L');
                    current = (current + 1 < length && word[current + 1] == 'L') ? current + 2 : current + 1;
                    break;

                case 'M':
                    primary.Append('M');
                    alternate.Append('M');
                    current = (current + 1 < length && word[current + 1] == 'M') ? current + 2 : current + 1;
                    break;

                case 'N':
                    primary.Append('N');
                    alternate.Append('N');
                    current = (current + 1 < length && word[current + 1] == 'N') ? current + 2 : current + 1;
                    break;

                case 'P':
                    if (current + 1 < length && word[current + 1] == 'H')
                    {
                        primary.Append('F');
                        alternate.Append('F');
                        current += 2;
                    }
                    else
                    {
                        primary.Append('P');
                        alternate.Append('P');
                        current = (current + 1 < length && (word[current + 1] == 'P' || word[current + 1] == 'B')) ? current + 2 : current + 1;
                    }
                    break;

                case 'Q':
                    primary.Append('K');
                    alternate.Append('K');
                    current = (current + 1 < length && word[current + 1] == 'Q') ? current + 2 : current + 1;
                    break;

                case 'R':
                    primary.Append('R');
                    alternate.Append('R');
                    current = (current + 1 < length && word[current + 1] == 'R') ? current + 2 : current + 1;
                    break;

                case 'S':
                    if (StringAt(word, current, 2, "SH"))
                    {
                        primary.Append('X');
                        alternate.Append('X');
                        current += 2;
                    }
                    else if (StringAt(word, current, 3, "SIO", "SIA"))
                    {
                        primary.Append('S');
                        alternate.Append('X');
                        current += 3;
                    }
                    else if (StringAt(word, current, 2, "SC"))
                    {
                        primary.Append('S');
                        alternate.Append('S');
                        current += 2;
                    }
                    else
                    {
                        primary.Append('S');
                        alternate.Append('S');
                        current = (current + 1 < length && word[current + 1] == 'S') ? current + 2 : current + 1;
                    }
                    break;

                case 'T':
                    if (StringAt(word, current, 4, "TION", "TIA"))
                    {
                        primary.Append('X');
                        alternate.Append('X');
                        current += 3;
                    }
                    else if (StringAt(word, current, 2, "TH"))
                    {
                        primary.Append('0');
                        alternate.Append('T');
                        current += 2;
                    }
                    else
                    {
                        primary.Append('T');
                        alternate.Append('T');
                        current = (current + 1 < length && (word[current + 1] == 'T' || word[current + 1] == 'D')) ? current + 2 : current + 1;
                    }
                    break;

                case 'V':
                    primary.Append('F');
                    alternate.Append('F');
                    current = (current + 1 < length && word[current + 1] == 'V') ? current + 2 : current + 1;
                    break;

                case 'W':
                    if (current + 1 < length && word[current + 1] == 'R')
                    {
                        primary.Append('R');
                        alternate.Append('R');
                        current += 2;
                    }
                    else if (current == 0 && (IsVowel(word, current + 1) || StringAt(word, current, 2, "WH")))
                    {
                        primary.Append('A');
                        alternate.Append('F');
                        current += 1;
                    }
                    else
                    {
                        current++;
                    }
                    break;

                case 'X':
                    primary.Append("KS");
                    alternate.Append("KS");
                    current = (current + 1 < length && word[current + 1] == 'X') ? current + 2 : current + 1;
                    break;

                case 'Z':
                    primary.Append('S');
                    alternate.Append('S');
                    current = (current + 1 < length && word[current + 1] == 'Z') ? current + 2 : current + 1;
                    break;

                default:
                    current++;
                    break;
            }
        }

        var p = primary.Length > MaxKeyLength ? primary.ToString(0, MaxKeyLength) : primary.ToString();
        var a = alternate.Length > MaxKeyLength ? alternate.ToString(0, MaxKeyLength) : alternate.ToString();

        return new MetaphoneResult(p, a);
    }

    public static double Compare(string s1, string s2)
    {
        if (string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase))
        {
            return 1.0;
        }

        var m1 = Encode(s1);
        var m2 = Encode(s2);

        if (string.IsNullOrEmpty(m1.Primary) || string.IsNullOrEmpty(m2.Primary))
        {
            return 0.0;
        }

        if (m1.Primary == m2.Primary)
        {
            return 1.0;
        }

        if (!string.IsNullOrEmpty(m1.Alternate) && m1.Alternate == m2.Primary ||
            !string.IsNullOrEmpty(m2.Alternate) && m1.Primary == m2.Alternate)
        {
            return 0.85;
        }

        if (!string.IsNullOrEmpty(m1.Alternate) && !string.IsNullOrEmpty(m2.Alternate) && m1.Alternate == m2.Alternate)
        {
            return 0.75;
        }

        return 0.0;
    }

    private static string CleanInput(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (char.IsLetter(c))
            {
                sb.Append(char.ToUpperInvariant(c));
            }
        }
        return sb.ToString();
    }

    private static bool IsVowel(string s, int index)
    {
        if (index < 0 || index >= s.Length)
        {
            return false;
        }
        var c = s[index];
        return c is 'A' or 'E' or 'I' or 'O' or 'U' or 'Y';
    }

    private static bool StringAt(string s, int start, int length, params string[] matches)
    {
        if (start < 0 || start + length > s.Length)
        {
            return false;
        }
        var sub = s.Substring(start, length);
        foreach (var m in matches)
        {
            if (string.Equals(sub, m, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }
}
