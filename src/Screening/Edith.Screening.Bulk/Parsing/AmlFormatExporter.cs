using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Edith.Screening.Bulk.Models;

namespace Edith.Screening.Bulk.Parsing;

public static class AmlFormatExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static (byte[] Content, string ContentType, string Extension) Export(
        IReadOnlyList<AmlEntityRecord> records,
        string format)
    {
        var normFormat = (format ?? "csv").Trim().TrimStart('.').ToLowerInvariant();

        return normFormat switch
        {
            "json" => (
                JsonSerializer.SerializeToUtf8Bytes(records, JsonOptions),
                "application/json",
                "json"
            ),
            "xml" => (
                Encoding.UTF8.GetBytes(ExportXml(records)),
                "application/xml",
                "xml"
            ),
            _ => (
                Encoding.UTF8.GetBytes(ExportCsv(records)),
                "text/csv",
                "csv"
            )
        };
    }

    private static string ExportCsv(IReadOnlyList<AmlEntityRecord> records)
    {
        var sb = new StringBuilder();
        sb.AppendLine("AML_TYPE,AML_FULLNAME,AML_IDDETAILS,AML_CATEGORY,AML_DATEOFBIRTH,AML_REMARKS,AML_NATIONALITY,AML_PROGRAM,AML_ALIASNAME");

        foreach (var r in records)
        {
            sb.Append(EscapeCsv(r.AmlType)).Append(',')
              .Append(EscapeCsv(r.AmlFullName)).Append(',')
              .Append(EscapeCsv(r.AmlIdDetails)).Append(',')
              .Append(EscapeCsv(r.AmlCategory)).Append(',')
              .Append(EscapeCsv(r.AmlDateOfBirth)).Append(',')
              .Append(EscapeCsv(r.AmlRemarks)).Append(',')
              .Append(EscapeCsv(r.AmlNationality)).Append(',')
              .Append(EscapeCsv(r.AmlProgram)).Append(',')
              .Append(EscapeCsv(r.AmlAliasName)).AppendLine();
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string? val)
    {
        if (string.IsNullOrEmpty(val)) return string.Empty;
        if (val.Contains(',') || val.Contains('"') || val.Contains('\n') || val.Contains('\r'))
        {
            return $"\"{val.Replace("\"", "\"\"")}\"";
        }
        return val;
    }

    private static string ExportXml(IReadOnlyList<AmlEntityRecord> records)
    {
        var root = new XElement("AML_DATASET",
            records.Select(r => new XElement("AML_RECORD",
                new XElement("AML_TYPE", r.AmlType ?? string.Empty),
                new XElement("AML_FULLNAME", r.AmlFullName),
                new XElement("AML_IDDETAILS", r.AmlIdDetails ?? string.Empty),
                new XElement("AML_CATEGORY", r.AmlCategory ?? string.Empty),
                new XElement("AML_DATEOFBIRTH", r.AmlDateOfBirth ?? string.Empty),
                new XElement("AML_REMARKS", r.AmlRemarks ?? string.Empty),
                new XElement("AML_NATIONALITY", r.AmlNationality ?? string.Empty),
                new XElement("AML_PROGRAM", r.AmlProgram ?? string.Empty),
                new XElement("AML_ALIASNAME", r.AmlAliasName ?? string.Empty)
            ))
        );

        return new XDeclaration("1.0", "utf-8", "yes").ToString() + Environment.NewLine + root.ToString();
    }
}
