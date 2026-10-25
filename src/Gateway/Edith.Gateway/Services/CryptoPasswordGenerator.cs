using System.Security.Cryptography;

namespace Edith.Gateway.Services;

public static class CryptoPasswordGenerator
{
    private const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // omitted I, O for clarity
    private const string Lowercase = "abcdefghijkmnopqrstuvwxyz"; // omitted l
    private const string Digits = "23456789"; // omitted 0, 1
    private const string Specials = "!@#$%^&*()-_=+[]{}";
    private const string AllChars = Uppercase + Lowercase + Digits + Specials;

    public static string GenerateSecurePassword(int length = 16)
    {
        if (length < 12) length = 12;

        var chars = new char[length];
        // Ensure at least one of each category
        chars[0] = Uppercase[RandomNumberGenerator.GetInt32(Uppercase.Length)];
        chars[1] = Lowercase[RandomNumberGenerator.GetInt32(Lowercase.Length)];
        chars[2] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];
        chars[3] = Specials[RandomNumberGenerator.GetInt32(Specials.Length)];

        for (int i = 4; i < length; i++)
        {
            chars[i] = AllChars[RandomNumberGenerator.GetInt32(AllChars.Length)];
        }

        // Shuffle the characters securely using Fisher-Yates
        for (int i = chars.Length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }

    public static string GenerateInvitationToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexStringLower(bytes);
    }
}
