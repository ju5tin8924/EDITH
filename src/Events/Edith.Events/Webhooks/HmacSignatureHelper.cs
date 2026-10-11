using System.Security.Cryptography;
using System.Text;

namespace Edith.Events.Webhooks;

public static class HmacSignatureHelper
{
    public static string ComputeSignature(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexStringLower(hash);
    }

    public static bool VerifySignature(string payload, string secret, string signature)
    {
        if (string.IsNullOrWhiteSpace(signature)) return false;

        var cleanSignature = signature.Trim();
        if (cleanSignature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            cleanSignature = cleanSignature["sha256=".Length..];
        }

        var expectedHex = ComputeSignature(payload, secret);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedHex);
        var actualBytes = Encoding.UTF8.GetBytes(cleanSignature);

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
