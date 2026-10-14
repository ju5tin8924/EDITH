using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Edith.Gateway.Endpoints;

public static class KycEndpoints
{
    private static readonly string[] BoutUbo = ["Viktor Anatolyevich Bout (100%)"];
    private static readonly string[] BarakaUbo = ["Tariq Mansoor Al-Ketbi (51%)", "Elena Rostova (49%)"];

    public static IEndpointRouteBuilder MapKycEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/kyc");

        group.MapGet("/{customerId}", (string customerId) =>
        {
            var isBout = customerId.Contains("001", StringComparison.OrdinalIgnoreCase) || customerId.Contains("bout", StringComparison.OrdinalIgnoreCase);

            var dossier = new
            {
                customerId = customerId,
                customerCode = isBout ? "CUST-UAE-9921448" : $"CUST-{customerId.ToUpperInvariant()}",
                fullName = isBout ? "Viktor Anatolyevich Bout" : "Al-Baraka General Trading LLC",
                riskCategory = isBout ? "EXTREME_SANCTIONS" : "HIGH_RISK_TRADING",
                riskScore = isBout ? 96.4 : 78.5,
                nationality = isBout ? "RU" : "AE",
                incorporationCountry = isBout ? "RU" : "AE",
                uboIdentities = isBout ? BoutUbo : BarakaUbo,
                documents = new[]
                {
                    new { id = "doc-1", title = "Passport / Commercial License", status = "VERIFIED_OCR", expiryDate = "2028-12-31" },
                    new { id = "doc-2", title = "Proof of Address (DEWA Bill)", status = "VERIFIED", expiryDate = "2026-11-30" },
                    new { id = "doc-3", title = "Audited Financials FY2025", status = "FLAGGED_ANOMALY", expiryDate = "2026-12-31" }
                },
                recentTransactions = new[]
                {
                    new { txId = "TX-9921448", date = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), amount = 450000.0, currency = "AED", counterparty = "Bank of Zurich", status = "FROZEN_HOLD" },
                    new { txId = "TX-9918231", date = DateTime.UtcNow.AddDays(-14).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), amount = 120000.0, currency = "AED", counterparty = "Dubai National Bank", status = "CLEARED" },
                    new { txId = "TX-9892019", date = DateTime.UtcNow.AddDays(-28).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), amount = 98000.0, currency = "AED", counterparty = "Emirates Islamic", status = "CLEARED" }
                }
            };

            return Results.Ok(dossier);
        }).AllowAnonymous();

        return app;
    }
}
