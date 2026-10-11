using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Edith.Gateway.Models;
using Microsoft.IdentityModel.Tokens;

namespace Edith.Gateway.Services;

public interface ITokenService
{
    TokenResponse GenerateToken(string username, string role, Guid tenantId);
}

public sealed class JwtTokenService : ITokenService
{
    public const string DefaultSecret = "EDITH_SUPER_SECURE_ENTERPRISE_AML_JWT_SECRET_KEY_2026_GCC_VALIDATION_STRING";
    public const string Issuer = "https://edith.aml.cloud";
    public const string Audience = "https://edith.aml.cloud";

    private readonly byte[] _keyBytes;

    public JwtTokenService(IConfiguration configuration)
    {
        var secret = configuration["Jwt:Secret"] ?? DefaultSecret;
        _keyBytes = Encoding.UTF8.GetBytes(secret);
    }

    public TokenResponse GenerateToken(string username, string role, Guid tenantId)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var expires = DateTime.UtcNow.AddHours(4);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.UniqueName, username),
            new(JwtRegisteredClaimNames.Name, username),
            new(JwtRegisteredClaimNames.Email, $"{username.ToLowerInvariant()}@compliance.edith.internal"),
            new(ClaimTypes.Role, role),
            new("role", role),
            new("tenant_id", tenantId.ToString()),
            new("tid", tenantId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = Issuer,
            Audience = Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(_keyBytes),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        return new TokenResponse
        {
            AccessToken = tokenString,
            TokenType = "Bearer",
            ExpiresIn = (int)TimeSpan.FromHours(4).TotalSeconds,
            TenantId = tenantId,
            Role = role
        };
    }
}
