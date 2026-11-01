using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Edith.Gateway.Models;
using Microsoft.IdentityModel.Tokens;

namespace Edith.Gateway.Services;

public interface ITokenService
{
    TokenResponse GenerateToken(string username, string role, Guid tenantId, TimeSpan? lifetime = null);
}

public sealed class JwtTokenService : ITokenService
{
    public const string Issuer = "https://edith.aml.cloud";
    public const string Audience = "https://edith.aml.cloud";

    private readonly byte[] _keyBytes;

    public JwtTokenService(IConfiguration configuration, IWebHostEnvironment? environment = null)
    {
        var secret = SecretsConfigurationManager.ResolveJwtSecret(configuration, environment ?? new DefaultDevHostEnvironment());
        _keyBytes = Encoding.UTF8.GetBytes(secret);
    }

    public TokenResponse GenerateToken(string username, string role, Guid tenantId, TimeSpan? lifetime = null)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var now = DateTime.UtcNow;
        var expires = now.Add(lifetime ?? TimeSpan.FromHours(4));
        var notBefore = expires < now ? expires.AddMinutes(-5) : now.AddMinutes(-1);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.UniqueName, username),
            new(JwtRegisteredClaimNames.Name, username),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
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
            NotBefore = notBefore,
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

    private sealed class DefaultDevHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Edith.Gateway";
        public string WebRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
