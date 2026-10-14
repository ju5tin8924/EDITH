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

    public static readonly Guid DefaultTenantId = Guid.Parse("01931000-0000-7000-8000-000000000000");
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
