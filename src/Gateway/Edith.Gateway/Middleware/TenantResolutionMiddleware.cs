using Edith.Gateway.Models;
using Edith.Gateway.Services;
using Microsoft.AspNetCore.Authorization;

namespace Edith.Gateway.Middleware;

public sealed class TenantResolutionMiddleware
{
    private static readonly HashSet<string> PublicPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health",
        "/healthz",
        "/api/v1/iam/login",
        "/api/v1/iam/mfa/verify",
        "/api/v1/iam/onboard/validate",
        "/api/v1/iam/mfa/setup",
        "/api/v1/iam/onboard/complete",
        "/api/v1/iam/token"
    };

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
        var path = context.Request.Path.Value ?? string.Empty;

        // Public authentication and health-check endpoints bypass strict tenant enforcement
        if (PublicPaths.Contains(path) || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Unauthenticated requests will be rejected by Authorization middleware
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // Tenant MUST be derived from validated JWT claims
        var claimTenantRaw = context.User.FindFirst("tenant_id")?.Value
                          ?? context.User.FindFirst("tid")?.Value;

        if (string.IsNullOrWhiteSpace(claimTenantRaw))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://edith.aml.cloud/errors/tenant-claim-missing",
                title = "Tenant Context Missing",
                status = StatusCodes.Status403Forbidden,
                detail = "The authenticated user identity does not contain an authoritative 'tenant_id' claim."
            });
            return;
        }

        // Forged or conflicting client header detection
        if (context.Request.Headers.TryGetValue("X-Tenant-ID", out var headerVal))
        {
            var headerTenantRaw = headerVal.ToString();
            if (!string.IsNullOrWhiteSpace(headerTenantRaw) &&
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
        }

        // Validate GUID syntax
        if (!Guid.TryParse(claimTenantRaw, out var tenantGuid))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://edith.aml.cloud/errors/invalid-tenant-id",
                title = "Invalid Tenant ID",
                status = StatusCodes.Status400BadRequest,
                detail = $"The token tenant identifier '{claimTenantRaw}' is not a valid GUID."
            });
            return;
        }

        // Validate tenant is registered and currently active
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

        // Establish ambient tenant context and propagate downstream
        tenantAccessor.CurrentTenant = tenant;
        context.Request.Headers["X-Tenant-ID"] = tenant.TenantId.ToString();

        await _next(context);
    }
}
