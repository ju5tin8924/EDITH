using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Edith.Gateway.Services;

public interface ITotpService
{
    string GenerateSecret(int byteLength = 20);
    string FormatOtpAuthUri(string username, string secretBase32, string issuer = "EDITH-AML");
    bool VerifyCode(string secretBase32, string code, int skewTolerance = 1);
    IReadOnlyList<string> GenerateRecoveryCodes(int count = 8);
    string HashRecoveryCode(string code);
    bool VerifyRecoveryCode(string code, string storedHash);
}

public sealed class TotpService : ITotpService
{
    private const string Base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public string GenerateSecret(int byteLength = 20)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return ToBase32(bytes);
    }

    public string FormatOtpAuthUri(string username, string secretBase32, string issuer = "EDITH-AML")
    {
        var escapedIssuer = Uri.EscapeDataString(issuer);
        var escapedUser = Uri.EscapeDataString(username);
        return $"otpauth://totp/{escapedIssuer}:{escapedUser}?secret={secretBase32}&issuer={escapedIssuer}&algorithm=SHA1&digits=6&period=30";
    }

    public bool VerifyCode(string secretBase32, string code, int skewTolerance = 1)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6) return false;
        if (!int.TryParse(code, out var expectedCode)) return false;

        byte[] key;
        try
        {
            key = FromBase32(secretBase32);
        }
        catch
        {
            return false;
        }

        var currentStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;

        for (int i = -skewTolerance; i <= skewTolerance; i++)
        {
            var step = currentStep + i;
            var computed = ComputeTotp(key, step);
            if (computed == expectedCode)
            {
                return true;
            }
        }

        return false;
    }

    public IReadOnlyList<string> GenerateRecoveryCodes(int count = 8)
    {
        var list = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            var raw = RandomNumberGenerator.GetBytes(4);
            var hex = Convert.ToHexString(raw);
            list.Add($"{hex[..4]}-{hex[4..]}");
        }
        return list;
    }

    public string HashRecoveryCode(string code)
    {
        var normalized = code.Trim().Replace("-", "").ToUpperInvariant();
        return IamPasswordHasher.HashPassword(normalized);
    }

    public bool VerifyRecoveryCode(string code, string storedHash)
    {
        var normalized = code.Trim().Replace("-", "").ToUpperInvariant();
        return IamPasswordHasher.VerifyPassword(normalized, storedHash);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms", Justification = "RFC 6238 TOTP specification standard requires HMAC-SHA1")]
    private static int ComputeTotp(byte[] key, long step)
    {
        Span<byte> stepBytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(stepBytes, step);

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(stepBytes.ToArray());

        int offset = hash[^1] & 0x0F;
        int binaryCode = ((hash[offset] & 0x7F) << 24)
                       | ((hash[offset + 1] & 0xFF) << 16)
                       | ((hash[offset + 2] & 0xFF) << 8)
                       | (hash[offset + 3] & 0xFF);

        return binaryCode % 1_000_000;
    }

    private static string ToBase32(byte[] bytes)
    {
        var sb = new StringBuilder((bytes.Length * 8 + 4) / 5);
        int buffer = 0;
        int bitsLeft = 0;

        foreach (byte b in bytes)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                sb.Append(Base32Chars[(buffer >> bitsLeft) & 0x1F]);
            }
        }

        if (bitsLeft > 0)
        {
            sb.Append(Base32Chars[(buffer << (5 - bitsLeft)) & 0x1F]);
        }

        return sb.ToString();
    }

    private static byte[] FromBase32(string base32)
    {
        var sanitized = base32.Trim().Replace(" ", "").Replace("-", "").ToUpperInvariant();
        var bytes = new List<byte>((sanitized.Length * 5) / 8);
        int buffer = 0;
        int bitsLeft = 0;

        foreach (char c in sanitized)
        {
            int val = Base32Chars.IndexOf(c);
            if (val < 0) continue;

            buffer = (buffer << 5) | val;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                bytes.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }

        return bytes.ToArray();
    }
}
