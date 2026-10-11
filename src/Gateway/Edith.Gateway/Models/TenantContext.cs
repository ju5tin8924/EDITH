namespace Edith.Gateway.Models;

public sealed record TenantContext
{
    public required Guid TenantId { get; init; }
    public required string TenantCode { get; init; }
    public required string TenantName { get; init; }
    public bool IsActive { get; init; } = true;
}

public interface ITenantContextAccessor
{
    TenantContext? CurrentTenant { get; set; }
}

public sealed class TenantContextAccessor : ITenantContextAccessor
{
    private static readonly AsyncLocal<TenantContext?> CurrentTenantHolder = new();

    public TenantContext? CurrentTenant
    {
        get => CurrentTenantHolder.Value;
        set => CurrentTenantHolder.Value = value;
    }
}
