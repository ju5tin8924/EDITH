using Edith.Gateway.Models;
using Edith.Gateway.Services;
using Microsoft.AspNetCore.Authorization;

namespace Edith.Gateway.Middleware;

public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantRegistry tenantRegistry,
        ITenantContextAccessor tenantAccessor)
    {
        var endpoint = context.GetEndpoint();
        var allowsAnonymous = endpoint?.Metadata.GetMetadata<IAllowAnonymous>() != null;

        var path = context.Request.Path.Value ?? string.Empty;
        if (allowsAnonymous || path.Equals("/health", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/api/v1/iam/token", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        string? claimTenantRaw = null;
        if (context.User.Identity?.IsAuthenticated == true)
        {
            claimTenantRaw = context.User.FindFirst("tenant_id")?.Value
                          ?? context.User.FindFirst("tid")?.Value;
        }

        string? headerTenantRaw = null;
        if (context.Request.Headers.TryGetValue("X-Tenant-ID", out var headerVal))
        {
            headerTenantRaw = headerVal.ToString();
        }

        // Cross-tenant mismatch check
        if (!string.IsNullOrWhiteSpace(claimTenantRaw) &&
            !string.IsNullOrWhiteSpace(headerTenantRaw) &&
            !string.Equals(claimTenantRaw, headerTenantRaw, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://edith.aml.cloud/errors/tenant-mismatch",
                title = "Tenant Access Denied",
                status = StatusCodes.Status403Forbidden,
                detail = "Cross-tenant mismatch: The provided X-Tenant-ID header does not match the authenticated token tenant."
            });
            return;
        }

        var tenantIdRaw = claimTenantRaw ?? headerTenantRaw;

        // Missing tenant context
        if (string.IsNullOrWhiteSpace(tenantIdRaw))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://edith.aml.cloud/errors/tenant-required",
                title = "Tenant Context Missing",
                status = StatusCodes.Status403Forbidden,
                detail = "A valid tenant identity is required via 'tenant_id' claim or 'X-Tenant-ID' header."
            });
            return;
        }

        // Validate GUID syntax
        if (!Guid.TryParse(tenantIdRaw, out var tenantGuid))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://edith.aml.cloud/errors/invalid-tenant-id",
                title = "Invalid Tenant ID",
                status = StatusCodes.Status400BadRequest,
                detail = $"The provided tenant identifier '{tenantIdRaw}' is not a valid GUID."
            });
            return;
        }

        // Validate tenant is registered and active
        var tenant = tenantRegistry.FindTenant(tenantGuid);
        if (tenant == null || !tenant.IsActive)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://edith.aml.cloud/errors/tenant-unauthorized",
                title = "Tenant Access Denied",
                status = StatusCodes.Status403Forbidden,
                detail = $"Tenant '{tenantGuid}' is not registered or has been deactivated."
            });
            return;
        }

        tenantAccessor.CurrentTenant = tenant;
        context.Request.Headers["X-Tenant-ID"] = tenant.TenantId.ToString();

        await _next(context);
    }
}
