using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Xml;
using Edith.Screening.Bulk.Models;

namespace Edith.Screening.Bulk.Parsing;

public interface IAmlStreamingParser
{
    IAsyncEnumerable<AmlEntityRecord> ParseStreamAsync(
        Stream stream,
        string format,
        CancellationToken ct = default);
}

public sealed class AmlStreamingParser : IAmlStreamingParser
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async IAsyncEnumerable<AmlEntityRecord> ParseStreamAsync(
        Stream stream,
        string format,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var normalizedFormat = format.Trim().TrimStart('.').ToLowerInvariant();

        switch (normalizedFormat)
        {
            case "csv":
                await foreach (var record in ParseCsvStreamAsync(stream, ct))
                {
                    yield return record;
                }
                break;

            case "json":
                await foreach (var record in ParseJsonStreamAsync(stream, ct))
                {
                    yield return record;
                }
                break;

            case "xml":
                await foreach (var record in ParseXmlStreamAsync(stream, ct))
                {
                    yield return record;
                }
                break;

            default:
                throw new NotSupportedException($"Unsupported bulk ingestion format '{format}'. Supported formats: CSV, JSON, XML.");
        }
    }

    private static async IAsyncEnumerable<AmlEntityRecord> ParseCsvStreamAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        string? headerLine = await reader.ReadLineAsync(ct);
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            yield break;
        }

        var headers = SplitCsvLine(headerLine).Select(h => h.Trim().ToUpperInvariant()).ToList();

        var typeIdx = headers.IndexOf("AML_TYPE");
        var nameIdx = headers.IndexOf("AML_FULLNAME");
        var idIdx = headers.IndexOf("AML_IDDETAILS");
        var catIdx = headers.IndexOf("AML_CATEGORY");
        var dobIdx = headers.IndexOf("AML_DATEOFBIRTH");
        var remIdx = headers.IndexOf("AML_REMARKS");
        var natIdx = headers.IndexOf("AML_NATIONALITY");
        var progIdx = headers.IndexOf("AML_PROGRAM");
        var aliasIdx = headers.IndexOf("AML_ALIASNAME");

        // Backward compatibility fallback for legacy CSV formats
        if (nameIdx < 0)
        {
            nameIdx = headers.IndexOf("BENEFICIARYNAME");
            if (nameIdx < 0) nameIdx = headers.IndexOf("FULL_NAME");
            if (nameIdx < 0) nameIdx = headers.IndexOf("NAME");
        }

        if (nameIdx < 0)
        {
            throw new FormatException("CSV header must contain 'AML_FULLNAME' or 'BeneficiaryName'.");
        }

        int rowIndex = 0;
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = SplitCsvLine(line);
            if (cols.Count <= nameIdx) continue;

            string fullName = cols[nameIdx].Trim();
            if (string.IsNullOrWhiteSpace(fullName)) continue;

            rowIndex++;
            yield return new AmlEntityRecord
            {
                RowIndex = rowIndex,
                AmlType = GetCol(cols, typeIdx),
                AmlFullName = fullName,
                AmlIdDetails = GetCol(cols, idIdx),
                AmlCategory = GetCol(cols, catIdx),
                AmlDateOfBirth = GetCol(cols, dobIdx),
                AmlRemarks = GetCol(cols, remIdx),
                AmlNationality = GetCol(cols, natIdx),
                AmlProgram = GetCol(cols, progIdx),
                AmlAliasName = GetCol(cols, aliasIdx)
            };
        }
    }

    private static async IAsyncEnumerable<AmlEntityRecord> ParseJsonStreamAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken ct)
    {
        int rowIndex = 0;
        await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<AmlEntityRecord>(stream, JsonOpts, ct))
        {
            if (item != null && !string.IsNullOrWhiteSpace(item.AmlFullName))
            {
                rowIndex++;
                item.RowIndex = rowIndex;
                yield return item;
            }
        }
    }

    private static async IAsyncEnumerable<AmlEntityRecord> ParseXmlStreamAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var settings = new XmlReaderSettings
        {
            Async = true,
            IgnoreWhitespace = true,
            DtdProcessing = DtdProcessing.Prohibit // Prevent XXE attacks
        };

        using var xmlReader = XmlReader.Create(stream, settings);
        int rowIndex = 0;

        while (await xmlReader.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();

            if (xmlReader.NodeType == XmlNodeType.Element &&
                (string.Equals(xmlReader.LocalName, "AML_RECORD", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(xmlReader.LocalName, "RECORD", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(xmlReader.LocalName, "ROW", StringComparison.OrdinalIgnoreCase)))
            {
                using var subtree = xmlReader.ReadSubtree();
                var el = await System.Xml.Linq.XElement.LoadAsync(subtree, System.Xml.Linq.LoadOptions.None, ct);

                var fullName = FindElementValue(el, "AML_FULLNAME") ?? FindElementValue(el, "BENEFICIARYNAME") ?? "";
                if (!string.IsNullOrWhiteSpace(fullName))
                {
                    rowIndex++;
                    yield return new AmlEntityRecord
                    {
                        RowIndex = rowIndex,
                        AmlType = FindElementValue(el, "AML_TYPE"),
                        AmlFullName = fullName,
                        AmlIdDetails = FindElementValue(el, "AML_IDDETAILS"),
                        AmlCategory = FindElementValue(el, "AML_CATEGORY"),
                        AmlDateOfBirth = FindElementValue(el, "AML_DATEOFBIRTH"),
                        AmlRemarks = FindElementValue(el, "AML_REMARKS"),
                        AmlNationality = FindElementValue(el, "AML_NATIONALITY"),
                        AmlProgram = FindElementValue(el, "AML_PROGRAM"),
                        AmlAliasName = FindElementValue(el, "AML_ALIASNAME")
                    };
                }
            }
        }
    }

    private static string? FindElementValue(System.Xml.Linq.XElement parent, string name)
    {
        var child = parent.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(child?.Value) ? null : child.Value.Trim();
    }

    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(sb.ToString().Trim());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        result.Add(sb.ToString().Trim());
        return result;
    }

    private static string? GetCol(List<string> cols, int index)
    {
        if (index >= 0 && index < cols.Count)
        {
            var val = cols[index].Trim();
            return string.IsNullOrEmpty(val) ? null : val;
        }
        return null;
    }
}
