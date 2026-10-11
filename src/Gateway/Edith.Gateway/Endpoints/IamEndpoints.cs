using System.Security.Claims;
using Edith.Gateway.Models;
using Edith.Gateway.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Edith.Gateway.Endpoints;

public static class IamEndpoints
{
    public static IEndpointRouteBuilder MapIamEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/iam");

        // 1. Issue Dev / S2S JWT Token
        group.MapPost("/token", (
            [FromBody] TokenRequest req,
            [FromServices] ITokenService tokenService,
            [FromServices] ITenantRegistry tenantRegistry) =>
        {
            var targetTenantId = req.TenantId ?? InMemoryTenantRegistry.DefaultTenantId;
            var tenant = tenantRegistry.FindTenant(targetTenantId);

            if (tenant == null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Unknown Tenant",
                    detail: $"Tenant '{targetTenantId}' is not registered.");
            }

            var role = !string.IsNullOrWhiteSpace(req.Role)
                ? req.Role.ToUpperInvariant()
                : EdithRoles.Mlro;

            if (!EdithRoles.All.Contains(role, StringComparer.OrdinalIgnoreCase))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Role",
                    detail: $"Role '{role}' is not a valid EDITH platform role.");
            }

            var tokenResponse = tokenService.GenerateToken(req.Username, role, tenant.TenantId);
            return Results.Ok(tokenResponse);
        }).AllowAnonymous();

        // 2. Query Current User Entitlements (RBAC + Menus)
        group.MapGet("/entitlements", (
            ClaimsPrincipal principal,
            [FromServices] IEntitlementsService entitlementsService,
            [FromServices] ITenantContextAccessor tenantAccessor) =>
        {
            var tenant = tenantAccessor.CurrentTenant;
            if (tenant == null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "No Active Tenant",
                    detail: "Could not resolve active tenant context for the current user.");
            }

            var userIdString = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? principal.FindFirst("sub")?.Value;
            var userId = Guid.TryParse(userIdString, out var parsedGuid) ? parsedGuid : Guid.NewGuid();

            var username = principal.FindFirst(ClaimTypes.Name)?.Value
                        ?? principal.FindFirst("name")?.Value
                        ?? principal.Identity?.Name
                        ?? "compliance.officer";

            var role = principal.FindFirst(ClaimTypes.Role)?.Value
                    ?? principal.FindFirst("role")?.Value
                    ?? EdithRoles.Analyst;

            var result = entitlementsService.GetEntitlements(userId, username, role, tenant);
            return Results.Ok(result);
        }).RequireAuthorization();

        // 3. List Active Tenants
        group.MapGet("/tenants", (
            [FromServices] ITenantRegistry tenantRegistry) =>
        {
            return Results.Ok(tenantRegistry.GetAllTenants());
        }).RequireAuthorization();

        return app;
    }
}
