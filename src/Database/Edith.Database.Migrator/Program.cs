using Edith.Database.Migrator;

// Usage:
//   dotnet run --project src/Database/Edith.Database.Migrator -- [--connection "<npgsql conn string>"] [--seed]
// Connection string precedence: --connection, then EDITH_PG_CONNECTION, then the local Docker Compose default.
const string LocalDevDefault = "Host=localhost;Port=5432;Database=edith_aml;Username=edith_admin;Password=EdithSecurePassword2026!";

var includeSeed = args.Contains("--seed");
var connIndex = Array.IndexOf(args, "--connection");
var connectionString =
    connIndex >= 0 && connIndex + 1 < args.Length ? args[connIndex + 1]
    : Environment.GetEnvironmentVariable("EDITH_PG_CONNECTION") ?? LocalDevDefault;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

try
{
    Console.WriteLine($"EDITH migrator: applying modules [{string.Join(" -> ", SqlMigrator.ModuleOrder)}]{(includeSeed ? " + dev seed" : "")}");
    await new SqlMigrator(connectionString, Console.Out).RunAsync(includeSeed, cts.Token);
    return 0;
}
catch (Exception ex) when (ex is Npgsql.NpgsqlException or InvalidOperationException or OperationCanceledException)
{
    Console.Error.WriteLine($"Migration failed: {ex.Message}");
    return 1;
}
