using Edith.Gateway.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Edith.Gateway.Endpoints;

public static class KycEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static IEndpointRouteBuilder MapKycEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/kyc");

        group.MapGet("/", async (
            [FromQuery] string? search,
            [FromQuery] bool? blockedOnly,
            [FromServices] IKycService kycService,
            CancellationToken ct) =>
        {
            var customers = await kycService.ListCustomersAsync(search, blockedOnly ?? false, ct);
            return Results.Ok(customers);
        }).AllowAnonymous();

        group.MapGet("/{customerId}", async (
            string customerId,
            [FromServices] IKycService kycService,
            CancellationToken ct) =>
        {
            var dossier = await kycService.GetCustomerDossierAsync(customerId, ct);
            if (dossier == null)
            {
                return Results.NotFound(new
                {
                    error = "CustomerNotFound",
                    message = $"Customer with identifier '{customerId}' was not found in the screening database."
                });
            }

            return Results.Ok(dossier);
        }).AllowAnonymous();

        group.MapPut("/{customerId}/edd", async (
            string customerId,
            [FromBody] KycEddUpdateRequest request,
            [FromServices] IKycService kycService,
            [FromServices] IAuditLedgerService auditService,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var updated = await kycService.UpdateCustomerEddAsync(customerId, request, ct);
            if (updated == null)
            {
                return Results.NotFound(new
                {
                    error = "CustomerNotFound",
                    message = $"Customer with identifier '{customerId}' was not found."
                });
            }

            var tenantHeader = httpContext.Request.Headers["X-Tenant-ID"].FirstOrDefault();
            var tenantId = Guid.TryParse(tenantHeader, out var parsedTid) ? parsedTid : DefaultTenantId;
            var actor = httpContext.User.Identity?.Name ?? "EDD Compliance Officer";

            try
            {
                await auditService.RecordAuditEntryAsync(tenantId, new RecordAuditRequest(
                    actor,
                    $"KYC_EDD_{request.Action.ToUpperInvariant()}",
                    $"{updated.FullName} ({updated.CustomerCode})",
                    null,
                    request.Notes ?? $"Updated KYC 360 EDD status ({request.Action}) for {updated.CustomerCode}. Risk Tier: {updated.RiskTier}, Blocked: {updated.IsBlocked}."
                ), ct);
            }
            catch
            {
                // Non-blocking if audit ledger DB is unreachable in offline test mode
            }

            return Results.Ok(updated);
        }).AllowAnonymous();

        return app;
    }
}
