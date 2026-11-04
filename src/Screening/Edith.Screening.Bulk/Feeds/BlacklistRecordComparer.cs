using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Edith.Screening.Bulk.Models;

namespace Edith.Screening.Bulk.Feeds;

public sealed record RecordComparisonDiff(
    bool IsExactMatch,
    string ComparisonResult,
    string Reason,
    IReadOnlyList<string> ChangedFields,
    string? PreviousDetailsJson,
    string? CurrentDetailsJson);

public static partial class BlacklistRecordComparer
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpaceRegex();

    public static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormKC);
        return MultiSpaceRegex().Replace(normalized, " ").ToUpperInvariant();
    }

    public static IReadOnlyList<string> CanonicalizeList(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return [];
        }

        return values
            .SelectMany(v => (v ?? string.Empty).Split([';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(NormalizeText)
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToList();
    }

    public static NormalizedBlacklistRecord CanonicalizeRecord(NormalizedBlacklistRecord record)
    {
        var aliases = CanonicalizeList(record.Aliases);
        var dobs = CanonicalizeList(record.DatesOfBirth);
        var ids = CanonicalizeList(record.Identifications);
        var addrs = CanonicalizeList(record.Addresses);
        var nats = CanonicalizeList(record.Nationalities);
        var progs = CanonicalizeList(record.Programs);

        DateOnly? primaryDob = record.PrimaryDob;
        if (!primaryDob.HasValue && dobs.Count > 0)
        {
            foreach (var d in dobs)
            {
                if (DateOnly.TryParse(d, out var parsed))
                {
                    primaryDob = parsed;
                    break;
                }
                if (d.Length >= 4 && int.TryParse(d.AsSpan(0, 4), out var yr) && yr is >= 1900 and <= 2100)
                {
                    primaryDob = new DateOnly(yr, 1, 1);
                    break;
                }
            }
        }

        return record with
        {
            SourceType = NormalizeText(record.SourceType),
            EntityExternalId = record.EntityExternalId.Trim(),
            EntityType = NormalizeEntityType(record.EntityType),
            FullName = MultiSpaceRegex().Replace((record.FullName ?? string.Empty).Trim(), " "),
            Aliases = aliases,
            DatesOfBirth = dobs,
            PrimaryDob = primaryDob,
            Identifications = ids,
            Addresses = addrs,
            Nationalities = nats,
            Programs = progs,
            Remarks = string.IsNullOrWhiteSpace(record.Remarks) ? null : MultiSpaceRegex().Replace(record.Remarks.Trim(), " ")
        };
    }

    public static string NormalizeEntityType(string? rawType)
    {
        var norm = NormalizeText(rawType);
        return norm switch
        {
            "INDIVIDUAL" or "PERSON" or "P" => "INDIVIDUAL",
            "ENTITY" or "CORPORATE" or "ENTERPRISE" or "E" or "ORGANIZATION" => "CORPORATE",
            "VESSEL" or "SHIP" => "VESSEL",
            "AIRCRAFT" => "AIRCRAFT",
            _ => string.IsNullOrEmpty(norm) ? "INDIVIDUAL" : norm
        };
    }

    public static string ComputeContentHash(NormalizedBlacklistRecord record)
    {
        var c = CanonicalizeRecord(record);
        var sb = new StringBuilder(512);
        sb.Append("SRC:").Append(c.SourceType).Append('|');
        sb.Append("ID:").Append(NormalizeText(c.EntityExternalId)).Append('|');
        sb.Append("TYPE:").Append(c.EntityType).Append('|');
        sb.Append("NAME:").Append(NormalizeText(c.FullName)).Append('|');
        sb.Append("AKA:").Append(string.Join(",", c.Aliases)).Append('|');
        sb.Append("DOB:").Append(string.Join(",", c.DatesOfBirth)).Append('|');
        sb.Append("IDS:").Append(string.Join(",", c.Identifications)).Append('|');
        sb.Append("ADDR:").Append(string.Join(",", c.Addresses)).Append('|');
        sb.Append("NAT:").Append(string.Join(",", c.Nationalities)).Append('|');
        sb.Append("PROG:").Append(string.Join(",", c.Programs)).Append('|');
        sb.Append("REM:").Append(NormalizeText(c.Remarks));

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexStringLower(bytes);
    }

    public static string ComputeBulkRecordHash(AmlEntityRecord record)
    {
        var sb = new StringBuilder(256);
        sb.Append("TYPE:").Append(NormalizeEntityType(record.AmlType)).Append('|');
        sb.Append("NAME:").Append(NormalizeText(record.AmlFullName)).Append('|');
        sb.Append("AKA:").Append(string.Join(",", CanonicalizeList(record.AmlAliasName is null ? [] : [record.AmlAliasName]))).Append('|');
        sb.Append("DOB:").Append(string.Join(",", CanonicalizeList(record.AmlDateOfBirth is null ? [] : [record.AmlDateOfBirth]))).Append('|');
        sb.Append("IDS:").Append(string.Join(",", CanonicalizeList(record.AmlIdDetails is null ? [] : [record.AmlIdDetails]))).Append('|');
        sb.Append("NAT:").Append(string.Join(",", CanonicalizeList(record.AmlNationality is null ? [] : [record.AmlNationality]))).Append('|');
        sb.Append("PROG:").Append(string.Join(",", CanonicalizeList(record.AmlProgram is null ? [] : [record.AmlProgram]))).Append('|');
        sb.Append("REM:").Append(NormalizeText(record.AmlRemarks));

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexStringLower(bytes);
    }

    public static string SerializeDetailsJson(NormalizedBlacklistRecord record)
    {
        var c = CanonicalizeRecord(record);
        var payload = new
        {
            entityExternalId = c.EntityExternalId,
            entityType = c.EntityType,
            fullName = c.FullName,
            aliases = c.Aliases,
            datesOfBirth = c.DatesOfBirth,
            identifications = c.Identifications,
            addresses = c.Addresses,
            nationalities = c.Nationalities,
            programs = c.Programs,
            remarks = c.Remarks ?? string.Empty
        };

        return JsonSerializer.Serialize(payload);
    }

    public static RecordComparisonDiff Compare(
        NormalizedBlacklistRecord? previousRecord,
        string? previousContentHash,
        bool wasPreviouslyActive,
        NormalizedBlacklistRecord currentRecord)
    {
        var canonicalCurrent = CanonicalizeRecord(currentRecord);
        var currentHash = ComputeContentHash(canonicalCurrent);
        var currentJson = SerializeDetailsJson(canonicalCurrent);

        if (previousRecord is null && string.IsNullOrEmpty(previousContentHash))
        {
            return new RecordComparisonDiff(
                IsExactMatch: false,
                ComparisonResult: "NEW_RECORD",
                Reason: $"New sanction designation added from {canonicalCurrent.SourceType} feed.",
                ChangedFields: ["ALL"],
                PreviousDetailsJson: null,
                CurrentDetailsJson: currentJson);
        }

        var prevJson = previousRecord is not null ? SerializeDetailsJson(previousRecord) : null;

        if (!wasPreviouslyActive)
        {
            return new RecordComparisonDiff(
                IsExactMatch: false,
                ComparisonResult: "REACTIVATED_RECORD",
                Reason: $"Previously removed {canonicalCurrent.SourceType} record reappeared in the latest source dataset and was reactivated.",
                ChangedFields: ["is_active", "attributes"],
                PreviousDetailsJson: prevJson,
                CurrentDetailsJson: currentJson);
        }

        if (!string.IsNullOrEmpty(previousContentHash) &&
            string.Equals(previousContentHash.Trim(), currentHash, StringComparison.OrdinalIgnoreCase))
        {
            return new RecordComparisonDiff(
                IsExactMatch: true,
                ComparisonResult: "EXACT_ATTRIBUTE_MATCH",
                Reason: "Existing active record matched all canonical attributes (names, aliases, DOBs, IDs, addresses, nationalities, programs/remarks) — no changes detected.",
                ChangedFields: [],
                PreviousDetailsJson: null,
                CurrentDetailsJson: null);
        }

        var changedFields = new List<string>();
        if (previousRecord is not null)
        {
            var prev = CanonicalizeRecord(previousRecord);
            if (!string.Equals(NormalizeText(prev.FullName), NormalizeText(canonicalCurrent.FullName), StringComparison.Ordinal))
                changedFields.Add("fullName");
            if (!string.Equals(prev.EntityType, canonicalCurrent.EntityType, StringComparison.Ordinal))
                changedFields.Add("entityType");
            if (!prev.Aliases.SequenceEqual(canonicalCurrent.Aliases, StringComparer.Ordinal))
                changedFields.Add("aliases");
            if (!prev.DatesOfBirth.SequenceEqual(canonicalCurrent.DatesOfBirth, StringComparer.Ordinal))
                changedFields.Add("datesOfBirth");
            if (!prev.Identifications.SequenceEqual(canonicalCurrent.Identifications, StringComparer.Ordinal))
                changedFields.Add("identifications");
            if (!prev.Addresses.SequenceEqual(canonicalCurrent.Addresses, StringComparer.Ordinal))
                changedFields.Add("addresses");
            if (!prev.Nationalities.SequenceEqual(canonicalCurrent.Nationalities, StringComparer.Ordinal))
                changedFields.Add("nationalities");
            if (!prev.Programs.SequenceEqual(canonicalCurrent.Programs, StringComparer.Ordinal))
                changedFields.Add("programs");
            if (!string.Equals(NormalizeText(prev.Remarks), NormalizeText(canonicalCurrent.Remarks), StringComparison.Ordinal))
                changedFields.Add("remarks");
        }

        if (changedFields.Count == 0 && previousRecord is not null)
        {
            return new RecordComparisonDiff(
                IsExactMatch: true,
                ComparisonResult: "EXACT_ATTRIBUTE_MATCH",
                Reason: "Existing active record matched all canonical attributes — only XML ordering/formatting differed.",
                ChangedFields: [],
                PreviousDetailsJson: null,
                CurrentDetailsJson: null);
        }

        if (changedFields.Count == 0)
        {
            changedFields.Add("attributes");
        }

        var fieldsSummary = string.Join(", ", changedFields);
        return new RecordComparisonDiff(
            IsExactMatch: false,
            ComparisonResult: $"CHANGED:{fieldsSummary}",
            Reason: $"Updated {changedFields.Count} attribute group(s): {fieldsSummary}.",
            ChangedFields: changedFields,
            PreviousDetailsJson: prevJson,
            CurrentDetailsJson: currentJson);
    }
}
