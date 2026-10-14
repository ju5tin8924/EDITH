using Edith.Addons.AiCopilot;
using Edith.Addons.GoAml;
using Edith.Addons.Graph;
using Edith.Cases.Models;
using Edith.Cases.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Edith.Gateway.Endpoints;

public static class AddonEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static IEndpointRouteBuilder MapAddonEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/addons");

        // 1. goAML XML Exporter
        group.MapPost("/goaml/export", async (
            [FromBody] CaseIdRequest req,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] ICaseService caseService,
            [FromServices] IGoAmlExporter goAmlExporter,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader);
            var parsedCaseId = Guid.TryParse(req.CaseId, out var g) ? g : Guid.Empty;
            var caseDetail = await caseService.GetCaseByIdAsync(tenantId, parsedCaseId, ct);

            caseDetail ??= new CaseDetail
            {
                Id = parsedCaseId != Guid.Empty ? parsedCaseId : Guid.NewGuid(),
                TenantId = tenantId,
                CaseNumber = "EDITH-CAS-2026-0042",
                TransactionId = Guid.NewGuid(),
                TransactionRefNo = "TX-2026-9921448",
                Amount = 450000.00m,
                Currency = "AED",
                SenderName = "Acme Global Trading LLC",
                BeneficiaryName = "Viktor Anatolyevich Bout",
                RiskScore = 96.4m,
                Priority = CasePriority.Critical,
                Status = CaseStatus.UnderReview,
                TriggeredList = "UN Security Council Res 1521 & OFAC SDN",
                HoldReason = "Arms trafficking designation under UN Resolution 1521",
                SlaDueDate = DateTimeOffset.UtcNow.AddDays(2)
            };

            var reportReq = new GoAmlReportRequest
            {
                ReportingEntityId = "CB_UAE_ADCB_001",
                SubmissionCode = $"SUB-{DateTime.UtcNow:yyyyMMdd}-{caseDetail.TransactionRefNo}",
                ReportType = "STR",
                CurrencyLocal = "AED",
                Case = caseDetail,
                Reason = $"Match against {caseDetail.TriggeredList} with composite confidence score {caseDetail.RiskScore}%",
                ActionTaken = "ACCOUNT_FROZEN"
            };

            var xml = goAmlExporter.GenerateGoAmlXml(reportReq);
            return Results.Content(xml, "application/xml");
        }).AllowAnonymous();

        // 2. AI STR Narrative Copilot
        group.MapPost("/ai/narrative", async (
            [FromBody] CaseIdRequest req,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] ICaseService caseService,
            [FromServices] IStrNarrativeCopilot copilot,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader);
            var parsedCaseId = Guid.TryParse(req.CaseId, out var g) ? g : Guid.Empty;
            var caseDetail = await caseService.GetCaseByIdAsync(tenantId, parsedCaseId, ct);

            caseDetail ??= new CaseDetail
            {
                Id = parsedCaseId != Guid.Empty ? parsedCaseId : Guid.NewGuid(),
                TenantId = tenantId,
                CaseNumber = "EDITH-CAS-2026-0042",
                TransactionId = Guid.NewGuid(),
                TransactionRefNo = "TX-2026-9921448",
                Amount = 450000.00m,
                Currency = "AED",
                SenderName = "Acme Global Trading LLC",
                BeneficiaryName = "Viktor Anatolyevich Bout",
                RiskScore = 96.4m,
                Priority = CasePriority.Critical,
                Status = CaseStatus.UnderReview,
                TriggeredList = "UN Security Council Res 1521 & OFAC SDN",
                HoldReason = "Arms trafficking designation under UN Resolution 1521",
                SlaDueDate = DateTimeOffset.UtcNow.AddDays(2)
            };

            var narrativeResult = copilot.DraftRegulatoryNarrative(caseDetail);

            return Results.Ok(new
            {
                caseNumber = narrativeResult.CaseNumber,
                narrative = narrativeResult.FullNarrativeMarkdown,
                sections = new
                {
                    introduction = narrativeResult.IntroductionSection,
                    transactionSummary = narrativeResult.TransactionSummarySection,
                    redFlags = narrativeResult.RedFlagsSection,
                    investigation = narrativeResult.InvestigationSection,
                    conclusion = narrativeResult.ConclusionSection
                }
            });
        }).AllowAnonymous();

        // 3. Entity Graph Visualizer
        group.MapGet("/graph/{caseId}", async (
            string caseId,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] ICaseService caseService,
            [FromServices] IEntityGraphVisualizer visualizer,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader);
            var parsedCaseId = Guid.TryParse(caseId, out var g) ? g : Guid.Empty;
            var caseDetail = await caseService.GetCaseByIdAsync(tenantId, parsedCaseId, ct);

            caseDetail ??= new CaseDetail
            {
                Id = parsedCaseId != Guid.Empty ? parsedCaseId : Guid.NewGuid(),
                TenantId = tenantId,
                CaseNumber = "EDITH-CAS-2026-0042",
                TransactionId = Guid.NewGuid(),
                TransactionRefNo = "TX-2026-9921448",
                Amount = 450000.00m,
                Currency = "AED",
                SenderName = "Acme Global Trading LLC",
                BeneficiaryName = "Viktor Anatolyevich Bout",
                RiskScore = 96.4m,
                Priority = CasePriority.Critical,
                Status = CaseStatus.UnderReview,
                TriggeredList = "UN Security Council Res 1521 & OFAC SDN",
                HoldReason = "Arms trafficking designation under UN Resolution 1521",
                SlaDueDate = DateTimeOffset.UtcNow.AddDays(2)
            };

            var graph = visualizer.BuildCaseEntityGraph(caseDetail, uboName: "Anatoly Bout Holdings Ltd", sharedPhone: "+971-50-992-1448");
            return Results.Ok(graph);
        }).AllowAnonymous();

        // 4. Dynamic Notifications Rules
        var notifGroup = app.MapGroup("/api/v1/notifications");
        notifGroup.MapGet("/rules", () =>
        {
            return Results.Ok(new[]
            {
                new
                {
                    id = "notif-1",
                    channel = "MS_TEAMS_WEBHOOK",
                    name = "Critical Watchlist Hit Channel",
                    target = "https://adcb.webhook.office.com/webhookb2/aml-critical-alerts",
                    severityThreshold = "CRITICAL",
                    isEnabled = true,
                    samplePayload = "AdaptiveCard: Critical OFAC Hit Triggered for Immediate CCO Review"
                },
                new
                {
                    id = "notif-2",
                    channel = "SECURE_EMAIL_SMTP",
                    name = "MLRO Escalation Dispatcher",
                    target = "mlro.office@adcb.ae",
                    severityThreshold = "HIGH",
                    isEnabled = true,
                    samplePayload = "Automated Escalation: 24h SLA Impending on Case EDITH-CAS-2026-0042"
                }
            });
        }).AllowAnonymous();

        return app;
    }

    private static Guid ResolveTenant(string? tenantHeader)
    {
        if (!string.IsNullOrWhiteSpace(tenantHeader) && Guid.TryParse(tenantHeader, out var parsed))
        {
            return parsed;
        }
        return DefaultTenantId;
    }
}

public sealed record CaseIdRequest(string CaseId);
