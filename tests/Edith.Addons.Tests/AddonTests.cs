using Edith.Addons.AiCopilot;
using Edith.Addons.GoAml;
using Edith.Addons.Graph;
using Edith.Addons.Notifications;
using Edith.Cases.Models;

namespace Edith.Addons.Tests;

public sealed class AddonTests
{
    private static readonly Guid TestTenantId = Guid.Parse("01931000-0000-7000-8000-000000000001");

    private static CaseDetail CreateSampleCase()
    {
        return new CaseDetail
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            CaseNumber = "CASE-20261011-8899",
            TransactionId = Guid.NewGuid(),
            TransactionRefNo = "TX-GOAML-001",
            Amount = 1500000m,
            Currency = "AED",
            SenderName = "Emirates Petrochem FZE",
            BeneficiaryName = "Viktor Anatolyevich Bout",
            RiskScore = 98.2m,
            Priority = CasePriority.Critical,
            Status = CaseStatus.Resolved,
            Resolution = CaseResolution.ReportedStrFreeze,
            ResolutionReason = "Confirmed OFAC SDN Sanction match",
            HoldReason = "OFAC_SDN SANCTION MATCH 98.2%",
            TriggeredList = "OFAC_SDN",
            SlaDueDate = DateTimeOffset.UtcNow.AddDays(3)
        };
    }

    [Fact]
    public void GoAmlExporter_Generates_Valid_XML_And_Passes_Validation()
    {
        var exporter = new GoAmlExporter();
        var sampleCase = CreateSampleCase();

        var request = new GoAmlReportRequest
        {
            ReportingEntityId = "UAE_FIU_RE_ENBD_1002",
            SubmissionCode = "STR-SUB-2026-0099",
            ReportType = "STR",
            CurrencyLocal = "AED",
            Case = sampleCase,
            Reason = "Beneficiary identified as designated individual on OFAC SDN list under sanctions regime.",
            ActionTaken = "ACCOUNT_FROZEN"
        };

        var xml = exporter.GenerateGoAmlXml(request);
        Assert.False(string.IsNullOrWhiteSpace(xml));
        Assert.Contains("<report>", xml);
        Assert.Contains("<rentity_id>UAE_FIU_RE_ENBD_1002</rentity_id>", xml);
        Assert.Contains("<submission_code>STR-SUB-2026-0099</submission_code>", xml);
        Assert.Contains("<transaction_number>TX-GOAML-001</transaction_number>", xml);
        Assert.Contains("<amount_local>1500000</amount_local>", xml);

        var isValid = exporter.ValidateGoAmlXml(xml, out var errors);
        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void GoAmlExporter_Detects_Invalid_Xml_Schema()
    {
        var exporter = new GoAmlExporter();
        const string badXml = "<invalid><broken_element></invalid>";

        var isValid = exporter.ValidateGoAmlXml(badXml, out var errors);
        Assert.False(isValid);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void StrNarrativeCopilot_Drafts_Standard_5_Paragraph_FATF_Narrative()
    {
        var copilot = new StrNarrativeCopilot();
        var sampleCase = CreateSampleCase();

        var notes = new List<CaseNote>
        {
            new()
            {
                Id = Guid.NewGuid(),
                CaseId = sampleCase.Id,
                AuthorId = Guid.NewGuid(),
                AuthorName = "Analyst Khalid",
                Content = "Verified passport scan does not match declared commercial trade documentation."
            }
        };

        var result = copilot.DraftRegulatoryNarrative(sampleCase, notes);

        Assert.NotNull(result);
        Assert.Equal(sampleCase.CaseNumber, result.CaseNumber);
        Assert.Contains(sampleCase.TransactionRefNo, result.IntroductionSection);
        Assert.Contains(sampleCase.BeneficiaryName, result.TransactionSummarySection);
        Assert.Contains("OFAC_SDN", result.RedFlagsSection);
        Assert.Contains("Analyst Khalid", result.InvestigationSection);
        Assert.Contains("Financial Intelligence Unit", result.ConclusionSection);
    }

    [Fact]
    public void DynamicNotificationEngine_Routes_To_OnDuty_Analyst_Directly()
    {
        var engine = new DynamicNotificationEngine();
        var analystId = Guid.NewGuid();

        var schedules = new List<ShiftSchedule>
        {
            new()
            {
                UserId = analystId,
                UserName = "Analyst Tariq",
                ShiftStart = new TimeOnly(8, 0),
                ShiftEnd = new TimeOnly(16, 0),
                IsOnDuty = true
            }
        };

        // Current time: 10:30 AM (on duty)
        var result = engine.RouteAlert(
            analystId,
            "Analyst Tariq",
            "Urgent Sanction Alert",
            "Critical wire freeze required",
            new TimeOnly(10, 30),
            schedules);

        Assert.Equal("DIRECT_ANALYST", result.TargetChannel);
        Assert.Equal("Analyst Tariq", result.RecipientName);
        Assert.False(result.WasEscalatedDueToShift);
    }

    [Fact]
    public void DynamicNotificationEngine_Escalates_When_Preferred_Analyst_Is_OffDuty()
    {
        var engine = new DynamicNotificationEngine();
        var analystId = Guid.NewGuid();
        var shiftSupervisorId = Guid.NewGuid();

        var schedules = new List<ShiftSchedule>
        {
            new()
            {
                UserId = analystId,
                UserName = "Analyst Tariq (Day)",
                ShiftStart = new TimeOnly(8, 0),
                ShiftEnd = new TimeOnly(16, 0),
                IsOnDuty = true
            },
            new()
            {
                UserId = shiftSupervisorId,
                UserName = "Supervisor Rashid (Night)",
                ShiftStart = new TimeOnly(16, 0),
                ShiftEnd = new TimeOnly(23, 59),
                IsOnDuty = true
            }
        };

        // Current time: 19:30 (7:30 PM - Tariq is off duty, Rashid is on duty)
        var result = engine.RouteAlert(
            analystId,
            "Analyst Tariq (Day)",
            "Urgent Sanction Alert",
            "Critical wire freeze required",
            new TimeOnly(19, 30),
            schedules);

        Assert.Equal("ON_DUTY_ANALYST", result.TargetChannel);
        Assert.Equal("Supervisor Rashid (Night)", result.RecipientName);
        Assert.True(result.WasEscalatedDueToShift);
        Assert.Contains("SHIFT ESCALATION", result.Message);
    }

    [Fact]
    public void EntityGraphVisualizer_Constructs_Cytoscape_Graph_With_UBO_And_SharedLinks()
    {
        var visualizer = new EntityGraphVisualizer();
        var sampleCase = CreateSampleCase();

        var graph = visualizer.BuildCaseEntityGraph(
            sampleCase,
            uboName: "Mikhail Semionovich",
            sharedPhone: "+971-50-9988776");

        Assert.NotNull(graph);
        // Nodes: Sender, Beneficiary, UBO, SharedPhone = 4 nodes
        Assert.Equal(4, graph.Nodes.Count);
        Assert.Contains(graph.Nodes, n => n.Data.NodeType == "SENDER");
        Assert.Contains(graph.Nodes, n => n.Data.NodeType == "BENEFICIARY");
        Assert.Contains(graph.Nodes, n => n.Data.NodeType == "UBO" && n.Data.Label == "Mikhail Semionovich");
        Assert.Contains(graph.Nodes, n => n.Data.NodeType == "SHARED_IDENTIFIER" && n.Data.Label == "+971-50-9988776");

        // Edges: TransfersTo, BeneficialOwnerOf, 2x SharedLink = 4 edges
        Assert.Equal(4, graph.Edges.Count);
        Assert.Contains(graph.Edges, e => e.Data.Relationship == "TRANSFERS_TO");
        Assert.Contains(graph.Edges, e => e.Data.Relationship == "BENEFICIAL_OWNER_OF");
        Assert.Contains(graph.Edges, e => e.Data.Relationship == "SHARED_LINK");
    }
}
