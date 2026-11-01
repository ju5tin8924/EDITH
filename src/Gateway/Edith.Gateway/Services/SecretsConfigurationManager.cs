using System.Text;

namespace Edith.Gateway.Services;

public static class SecretsConfigurationManager
{
    public const string InsecureDefaultJwtSecret = "EDITH_SUPER_SECURE_ENTERPRISE_AML_JWT_SECRET_KEY_2026_GCC_VALIDATION_STRING";
    public const string InsecureDefaultPostgresPassword = "EdithSecurePassword2026!";
    public const string VaultSecretsDefaultDir = "/vault/secrets";

    public static string ResolveJwtSecret(IConfiguration configuration, IWebHostEnvironment environment)
    {
        // 1. Vault Agent secret files (e.g. /vault/secrets/jwt.env)
        var vaultSecret = TryReadVaultSecret("jwt.env", "JWT_SECRET") 
                       ?? TryReadVaultSecret("secrets.env", "JWT_SECRET");

        // 2. Direct Environment Variables
        var envSecret = Environment.GetEnvironmentVariable("JWT_SECRET") 
                     ?? Environment.GetEnvironmentVariable("JWT_SIGNING_KEY");

        // 3. Application Configuration
        var configSecret = configuration["Jwt:Secret"] ?? configuration["JWT_SECRET"];

        var resolved = vaultSecret ?? envSecret ?? configSecret;

        if (environment.IsProduction())
        {
            if (string.IsNullOrWhiteSpace(resolved) || resolved.Length < 32)
            {
                throw new InvalidOperationException(
                    "CRITICAL SECURITY CONFIGURATION ERROR: 'Jwt:Secret' must be provided with a minimum length of 32 characters (256 bits) in Production. Static development secrets are strictly prohibited.");
            }

            if (resolved.Equals(InsecureDefaultJwtSecret, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "CRITICAL SECURITY CONFIGURATION ERROR: Deprecated hardcoded development JWT secret detected in Production configuration. Rotate secrets through HashiCorp Vault or environment injection.");
            }
        }

        return resolved ?? "DevelopmentOnlyLocalHmacKeyForTestingPurposesMustBeAtLeast32Bytes!";
    }

    public static string? ResolvePostgreSqlConnectionString(IConfiguration configuration, IWebHostEnvironment environment)
    {
        // 1. Dynamic credentials from HashiCorp Vault Agent template (/vault/secrets/database.env)
        var vaultDbFile = Path.Combine(VaultSecretsDefaultDir, "database.env");
        if (File.Exists(vaultDbFile))
        {
            try
            {
                var lines = File.ReadAllLines(vaultDbFile);
                string? user = null;
                string? pass = null;
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("POSTGRES_USER=", StringComparison.OrdinalIgnoreCase))
                    {
                        user = trimmed["POSTGRES_USER=".Length..].Trim('"', '\'');
                    }
                    else if (trimmed.StartsWith("POSTGRES_PASSWORD=", StringComparison.OrdinalIgnoreCase))
                    {
                        pass = trimmed["POSTGRES_PASSWORD=".Length..].Trim('"', '\'');
                    }
                }

                if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(pass))
                {
                    var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? configuration["Config:PostgresHost"] ?? "localhost";
                    var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? configuration["Config:PostgresPort"] ?? "5432";
                    var db = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? configuration["Config:PostgresDatabase"] ?? "edith_production";
                    return $"Host={host};Port={port};Database={db};Username={user};Password={pass};";
                }
            }
            catch
            {
                // Fall through to standard env / config
            }
        }

        // 2. Direct Environment Variable
        var envConn = Environment.GetEnvironmentVariable("EDITH_PG_CONNECTION");

        // 3. Application Configuration ConnectionStrings
        var configConn = configuration.GetConnectionString("EdithPostgres")
                      ?? configuration["EDITH_PG_CONNECTION"]
                      ?? configuration["ConnectionStrings:EdithPostgres"];

        var resolved = envConn ?? configConn;

        if (environment.IsProduction())
        {
            if (string.IsNullOrWhiteSpace(resolved))
            {
                throw new InvalidOperationException(
                    "CRITICAL SECURITY CONFIGURATION ERROR: Database connection string is missing in Production. Supply credentials via HashiCorp Vault Agent or 'EDITH_PG_CONNECTION'.");
            }

            if (resolved.Contains(InsecureDefaultPostgresPassword, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "CRITICAL SECURITY CONFIGURATION ERROR: Known default database password detected in Production. Real database credentials must be configured.");
            }
        }

        return resolved;
    }

    public static void ValidateProductionConfiguration(IConfiguration configuration, IWebHostEnvironment environment)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        // Validate JWT & Database Secrets in Production
        _ = ResolveJwtSecret(configuration, environment);
        _ = ResolvePostgreSqlConnectionString(configuration, environment);
    }

    private static string? TryReadVaultSecret(string fileName, string key)
    {
        var filePath = Path.Combine(VaultSecretsDefaultDir, fileName);
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var lines = File.ReadAllLines(filePath);
            var prefix = $"{key}=";
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed[prefix.Length..].Trim('"', '\'');
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }
}
