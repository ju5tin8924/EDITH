using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Edith.Screening.Bulk.Models;

/// <summary>
/// Domain model and DTO representing an ingested entity row across CSV, XML, and JSON formats.
/// </summary>
[XmlRoot("AML_RECORD")]
public sealed record AmlEntityRecord
{
    [XmlIgnore]
    [JsonIgnore]
    public int RowIndex { get; set; }

    [JsonPropertyName("AML_TYPE")]
    [XmlElement("AML_TYPE")]
    public string? AmlType { get; set; }

    [JsonPropertyName("AML_FULLNAME")]
    [XmlElement("AML_FULLNAME")]
    public string AmlFullName { get; set; } = string.Empty;

    [JsonPropertyName("AML_IDDETAILS")]
    [XmlElement("AML_IDDETAILS")]
    public string? AmlIdDetails { get; set; }

    [JsonPropertyName("AML_UID")]
    public string? AmlUid
    {
        get => AmlIdDetails;
        set => AmlIdDetails ??= value;
    }

    [JsonPropertyName("AML_CATEGORY")]
    [XmlElement("AML_CATEGORY")]
    public string? AmlCategory { get; set; }

    [JsonPropertyName("AML_DATEOFBIRTH")]
    [XmlElement("AML_DATEOFBIRTH")]
    public string? AmlDateOfBirth { get; set; }

    [JsonPropertyName("AML_REMARKS")]
    [XmlElement("AML_REMARKS")]
    public string? AmlRemarks { get; set; }

    [JsonPropertyName("AML_NATIONALITY")]
    [XmlElement("AML_NATIONALITY")]
    public string? AmlNationality { get; set; }

    [JsonPropertyName("AML_PROGRAM")]
    [XmlElement("AML_PROGRAM")]
    public string? AmlProgram { get; set; }

    [JsonPropertyName("AML_ALIASNAME")]
    [XmlElement("AML_ALIASNAME")]
    public string? AmlAliasName { get; set; }
}

[XmlRoot("AML_DATASET")]
public sealed class AmlXmlDataset
{
    [XmlElement("AML_RECORD")]
    public List<AmlEntityRecord> Records { get; set; } = [];
}
