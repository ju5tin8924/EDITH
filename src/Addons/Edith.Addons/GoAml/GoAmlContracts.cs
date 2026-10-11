using System.Globalization;
using System.Xml.Linq;
using Edith.Cases.Models;

namespace Edith.Addons.GoAml;

public sealed record GoAmlReportRequest
{
    public required string ReportingEntityId { get; init; } // e.g. "UAE_FIU_RE_1002"
    public required string SubmissionCode { get; init; }    // e.g. "SUB-20261011-001"
    public string ReportType { get; init; } = "STR";        // STR (Suspicious Transaction Report)
    public string CurrencyLocal { get; init; } = "AED";
    public required CaseDetail Case { get; init; }
    public required string Reason { get; init; }
    public string ActionTaken { get; init; } = "ACCOUNT_FROZEN";
}

public interface IGoAmlExporter
{
    string GenerateGoAmlXml(GoAmlReportRequest request);
    bool ValidateGoAmlXml(string xmlContent, out List<string> validationErrors);
}

public sealed class GoAmlExporter : IGoAmlExporter
{
    public string GenerateGoAmlXml(GoAmlReportRequest request)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement("report",
                new XElement("rentity_id", request.ReportingEntityId),
                new XElement("submission_code", request.SubmissionCode),
                new XElement("report_code", request.ReportType),
                new XElement("submission_date", DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)),
                new XElement("currency_code_local", request.CurrencyLocal),
                new XElement("reason", request.Reason),
                new XElement("action", request.ActionTaken),
                new XElement("transaction",
                    new XElement("transaction_number", request.Case.TransactionRefNo),
                    new XElement("transaction_location", "UAE"),
                    new XElement("date_transaction", request.Case.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)),
                    new XElement("amount_local", request.Case.Amount),
                    new XElement("t_from_my_client",
                        new XElement("from_funds_code", "WIRE"),
                        new XElement("from_entity",
                            new XElement("name", request.Case.SenderName)
                        )
                    ),
                    new XElement("t_to",
                        new XElement("to_funds_code", "WIRE"),
                        new XElement("to_entity",
                            new XElement("name", request.Case.BeneficiaryName)
                        )
                    )
                ),
                new XElement("report_indicators",
                    new XElement("indicator", request.Case.TriggeredList ?? "AML_SUSPICION"),
                    new XElement("risk_score", request.Case.RiskScore)
                )
            )
        );

        return doc.Declaration + Environment.NewLine + doc.ToString();
    }

    public bool ValidateGoAmlXml(string xmlContent, out List<string> validationErrors)
    {
        validationErrors = [];
        try
        {
            var doc = XDocument.Parse(xmlContent);
            var root = doc.Root;

            if (root == null || root.Name != "report")
            {
                validationErrors.Add("Root element must be '<report>'.");
                return false;
            }

            string[] requiredTags = ["rentity_id", "submission_code", "report_code", "transaction", "reason"];
            foreach (var tag in requiredTags)
            {
                if (root.Element(tag) == null || string.IsNullOrWhiteSpace(root.Element(tag)?.Value))
                {
                    validationErrors.Add($"Missing required goAML element '<{tag}>'.");
                }
            }

            return validationErrors.Count == 0;
        }
        catch (Exception ex)
        {
            validationErrors.Add($"Invalid XML format: {ex.Message}");
            return false;
        }
    }
}
