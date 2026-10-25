using System.Collections.Concurrent;
using Edith.Gateway.Models;

namespace Edith.Gateway.Services;

public interface ITenantRegistry
{
    TenantContext? FindTenant(Guid tenantId);
    void RegisterTenant(TenantContext tenant);
    IReadOnlyList<TenantContext> GetAllTenants();
}

public sealed class InMemoryTenantRegistry : ITenantRegistry
{
    private readonly ConcurrentDictionary<Guid, TenantContext> _tenants = new();

    public static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid SecondaryTenantId = Guid.Parse("01931000-0000-7000-8000-000000000002");
    public static readonly Guid DemoTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public InMemoryTenantRegistry()
    {
        var primaryTenant = new TenantContext
        {
            TenantId = DefaultTenantId,
            TenantCode = "ENBD_UAE",
            TenantName = "Emirates NBD Commercial Banking",
            IsActive = true
        };

        var secondaryTenant = new TenantContext
        {
            TenantId = SecondaryTenantId,
            TenantCode = "ADCB_UAE",
            TenantName = "Abu Dhabi Commercial Bank",
            IsActive = true
        };

        var demoTenant = new TenantContext
        {
            TenantId = DemoTenantId,
            TenantCode = "ADCB_DEMO",
            TenantName = "ADCB Compliance Unit 04",
            IsActive = true
        };

        _tenants[primaryTenant.TenantId] = primaryTenant;
        _tenants[secondaryTenant.TenantId] = secondaryTenant;
        _tenants[demoTenant.TenantId] = demoTenant;
    }

    public TenantContext? FindTenant(Guid tenantId)
    {
        return _tenants.TryGetValue(tenantId, out var tenant) ? tenant : null;
    }

    public void RegisterTenant(TenantContext tenant)
    {
        _tenants[tenant.TenantId] = tenant;
    }

    public IReadOnlyList<TenantContext> GetAllTenants()
    {
        return _tenants.Values.ToList();
    }
}

public sealed class PostgreSqlTenantRegistry : ITenantRegistry
{
    private readonly string _connectionString;

    public PostgreSqlTenantRegistry(string connectionString)
    {
        _connectionString = connectionString;
        EnsureBaselineTenants();
    }

    private void EnsureBaselineTenants()
    {
        RegisterTenant(new TenantContext
        {
            TenantId = InMemoryTenantRegistry.DefaultTenantId,
            TenantCode = "ENBD_UAE",
            TenantName = "Emirates NBD Commercial Banking",
            IsActive = true
        });
        RegisterTenant(new TenantContext
        {
            TenantId = InMemoryTenantRegistry.SecondaryTenantId,
            TenantCode = "ADCB_UAE",
            TenantName = "Abu Dhabi Commercial Bank",
            IsActive = true
        });
        RegisterTenant(new TenantContext
        {
            TenantId = InMemoryTenantRegistry.DemoTenantId,
            TenantCode = "EGB_UAE",
            TenantName = "Emirates Global Bank",
            IsActive = true
        });
    }

    public TenantContext? FindTenant(Guid tenantId)
    {
        using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = new Npgsql.NpgsqlCommand(
            "SELECT id, name FROM core.tenants WHERE id = @id LIMIT 1", conn);
        cmd.Parameters.AddWithValue("id", tenantId);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        var name = r.GetString(1);
        return new TenantContext
        {
            TenantId = r.GetGuid(0),
            TenantCode = name.ToUpperInvariant().Replace(' ', '_'),
            TenantName = name,
            IsActive = true
        };
    }

    public void RegisterTenant(TenantContext tenant)
    {
        using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = new Npgsql.NpgsqlCommand(@"
            INSERT INTO core.tenants (id, name)
            VALUES (@id, @name)
            ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name;", conn);
        cmd.Parameters.AddWithValue("id", tenant.TenantId);
        cmd.Parameters.AddWithValue("name", tenant.TenantName);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<TenantContext> GetAllTenants()
    {
        using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = new Npgsql.NpgsqlCommand(
            "SELECT id, name FROM core.tenants ORDER BY name", conn);
        using var r = cmd.ExecuteReader();
        var list = new List<TenantContext>();
        while (r.Read())
        {
            var name = r.GetString(1);
            list.Add(new TenantContext
            {
                TenantId = r.GetGuid(0),
                TenantCode = name.ToUpperInvariant().Replace(' ', '_'),
                TenantName = name,
                IsActive = true
            });
        }
        return list;
    }
}

