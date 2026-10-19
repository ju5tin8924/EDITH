using Edith.Gateway.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Edith.Gateway.Endpoints;

public static class KycEndpoints
{
    public static IEndpointRouteBuilder MapKycEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/kyc");

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

        return app;
    }
}
