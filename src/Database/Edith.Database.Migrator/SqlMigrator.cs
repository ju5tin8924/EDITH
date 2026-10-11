using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace Edith.Database.Migrator;

/// <summary>A single embedded SQL script and its identity in the migration history.</summary>
internal sealed record MigrationScript(string Id, string Module, string Sql, string Checksum);

/// <summary>
/// Applies embedded, versioned SQL scripts to PostgreSQL in module dependency order.
/// Each script runs in its own transaction and is recorded in <c>public.edith_schema_migrations</c>.
/// If an already-applied script has been edited, the run stops instead of silently diverging.
/// </summary>
internal sealed class SqlMigrator
{
    /// <summary>
    /// Module order follows the cross-schema foreign keys in DDL v1.0:
    /// rules/screening/cases/notifications reference core; screening references rules; cases references screening and rules.
    /// </summary>
    public static readonly IReadOnlyList<string> ModuleOrder = ["core", "rules", "screening", "cases", "notifications"];

    private const string SeedModulePrefix = "seed:";

    // Arbitrary fixed key so concurrent migrator instances serialize instead of racing.
    private const long MigrationLockKey = 0x4544_4954_4800_0001;

    private readonly string _connectionString;
    private readonly TextWriter _log;

    public SqlMigrator(string connectionString, TextWriter log)
    {
        _connectionString = connectionString;
        _log = log;
    }

    public async Task<int> RunAsync(bool includeDevSeed, CancellationToken ct)
    {
        var scripts = LoadScripts(includeDevSeed);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await ExecuteAsync(conn, null, $"SELECT pg_advisory_lock({MigrationLockKey});", ct);
        try
        {
            await ExecuteAsync(conn, null, """
                CREATE TABLE IF NOT EXISTS public.edith_schema_migrations (
                    script_id  TEXT PRIMARY KEY,
                    module     TEXT NOT NULL,
                    checksum   CHAR(64) NOT NULL,
                    applied_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
                """, ct);

            var applied = await LoadAppliedAsync(conn, ct);
            var appliedCount = 0;

            foreach (var script in scripts)
            {
                if (applied.TryGetValue(script.Id, out var existingChecksum))
                {
                    if (!string.Equals(existingChecksum, script.Checksum, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Checksum mismatch for already-applied script '{script.Id}'. " +
                            "Applied migrations must not be edited; add a new script instead.");
                    }

                    continue;
                }

                await using var tx = await conn.BeginTransactionAsync(ct);
                await ExecuteAsync(conn, tx, script.Sql, ct);

                await using (var record = new NpgsqlCommand(
                    "INSERT INTO public.edith_schema_migrations (script_id, module, checksum) VALUES (@id, @module, @checksum);",
                    conn, tx))
                {
                    record.Parameters.AddWithValue("id", script.Id);
                    record.Parameters.AddWithValue("module", script.Module);
                    record.Parameters.AddWithValue("checksum", script.Checksum);
                    await record.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
                appliedCount++;
                await _log.WriteLineAsync($"  applied  {script.Id}");
            }

            await _log.WriteLineAsync(
                $"Done. {appliedCount} script(s) applied, {scripts.Count - appliedCount} already up to date.");
            return appliedCount;
        }
        finally
        {
            await ExecuteAsync(conn, null, $"SELECT pg_advisory_unlock({MigrationLockKey});", CancellationToken.None);
        }
    }

    /// <summary>Loads embedded scripts in apply order: modules by <see cref="ModuleOrder"/>, then by file name.</summary>
    internal static List<MigrationScript> LoadScripts(bool includeDevSeed)
    {
        var assembly = typeof(SqlMigrator).Assembly;
        var resources = assembly.GetManifestResourceNames()
            .Select(name => name.Replace('\\', '/'))
            .ToList();

        var migrations = new List<MigrationScript>();
        foreach (var name in resources.Where(n => n.StartsWith("Scripts/", StringComparison.Ordinal)))
        {
            var parts = name.Split('/');
            if (parts.Length != 3 || !ModuleOrder.Contains(parts[1]))
            {
                throw new InvalidOperationException(
                    $"Script '{name}' is not under a known module folder ({string.Join(", ", ModuleOrder)}).");
            }

            migrations.Add(Load(assembly, name, module: parts[1]));
        }

        var ordered = migrations
            .OrderBy(s => IndexOfModule(s.Module))
            .ThenBy(s => s.Id, StringComparer.Ordinal)
            .ToList();

        if (includeDevSeed)
        {
            ordered.AddRange(resources
                .Where(n => n.StartsWith("Seeds/dev/", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .Select(n => Load(assembly, n, module: SeedModulePrefix + "dev")));
        }

        return ordered;
    }

    private static int IndexOfModule(string module)
    {
        for (var i = 0; i < ModuleOrder.Count; i++)
        {
            if (ModuleOrder[i] == module)
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    private static MigrationScript Load(Assembly assembly, string normalizedName, string module)
    {
        var resourceName = assembly.GetManifestResourceNames()
            .Single(n => n.Replace('\\', '/') == normalizedName);

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var sql = reader.ReadToEnd();

        return new MigrationScript(normalizedName, module, sql, ComputeChecksum(sql));
    }

    /// <summary>
    /// SHA-256 over the script with line endings normalized, so a git CRLF/LF checkout
    /// doesn't count as an edit.
    /// </summary>
    internal static string ComputeChecksum(string sql)
    {
        var normalized = sql.Replace("\r\n", "\n", StringComparison.Ordinal);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    private static async Task<Dictionary<string, string>> LoadAppliedAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        var applied = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var cmd = new NpgsqlCommand("SELECT script_id, checksum FROM public.edith_schema_migrations;", conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            applied[reader.GetString(0)] = reader.GetString(1);
        }

        return applied;
    }

    private static async Task ExecuteAsync(NpgsqlConnection conn, NpgsqlTransaction? tx, string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
