using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Edith.Gateway.Endpoints;

public static class SettingsEndpoints
{
    private static readonly string[] BlackListCountries = ["IR", "KP", "MM"];
    private static readonly string[] GreyListCountries = ["SY", "YE", "SS", "CD"];
    private static readonly string[] MonitoredCountries = ["RU", "BY", "VE"];

    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var settingsGroup = app.MapGroup("/api/v1/settings");
        var reportsGroup = app.MapGroup("/api/v1/reports");

        // 1. System Masters
        settingsGroup.MapGet("/masters", () =>
        {
            return Results.Ok(new
            {
                fatfTiers = new[]
                {
                    new { tier = "BLACK_LIST", countries = BlackListCountries, riskFactor = 100 },
                    new { tier = "GREY_LIST", countries = GreyListCountries, riskFactor = 75 },
                    new { tier = "MONITORED", countries = MonitoredCountries, riskFactor = 60 }
                },
                currencyThresholds = new[]
                {
                    new { currency = "AED", mandatoryReportingThreshold = 55000, highRiskApprovalThreshold = 500000 },
                    new { currency = "USD", mandatoryReportingThreshold = 10000, highRiskApprovalThreshold = 100000 },
                    new { currency = "EUR", mandatoryReportingThreshold = 10000, highRiskApprovalThreshold = 100000 }
                },
                scoringWeights = new
                {
                    nameWeightage = 60.0,
                    nationalityWeightage = 10.0,
                    dobWeightage = 15.0,
                    idWeightage = 15.0,
                    defaultThreshold = 80.0
                }
            });
        }).AllowAnonymous();

        // 2. WORM Immutable Audit Trail
        reportsGroup.MapGet("/audit", () =>
        {
            return Results.Ok(new[]
            {
                new
                {
                    id = "aud-worm-001",
                    timestampUtc = DateTimeOffset.UtcNow.AddHours(-1).ToString("o"),
                    actor = "Fatima Al-Nuaimi (MLRO)",
                    action = "ASSET_FREEZE_ORDER",
                    targetEntity = "Viktor Anatolyevich Bout",
                    caseNumber = "EDITH-CAS-2026-0042",
                    sha256Hash = "8e98f7e2a9c3d4f1084b6f9a0c2e4f6a8b1c3d5e7f9a1b3c5d7e9f1a3b5c7d9e",
                    previousBlockHash = "7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b7c8d9e0f1a2b3c4d5e6f7a8b",
                    isTamperEvident = true
                },
                new
                {
                    id = "aud-worm-002",
                    timestampUtc = DateTimeOffset.UtcNow.AddHours(-3).ToString("o"),
                    actor = "SYSTEM_SCREENING_CORE",
                    action = "SANCTIONS_HOLD_INTERCEPT",
                    targetEntity = "Acme Global Trading LLC",
                    caseNumber = "EDITH-CAS-2026-0042",
                    sha256Hash = "7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b7c8d9e0f1a2b3c4d5e6f7a8b",
                    previousBlockHash = "1f2e3d4c5b6a7f8e9d0c1b2a3f4e5d6c7b8a9f0e1d2c3b4a5f6e7d8c9b0a1f2e",
                    isTamperEvident = true
                }
            });
        }).AllowAnonymous();

        return app;
    }
}
