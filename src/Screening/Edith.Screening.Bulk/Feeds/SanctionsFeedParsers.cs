using System.Xml;
using System.Xml.Linq;

namespace Edith.Screening.Bulk.Feeds;

public sealed record SanctionsFeedParseOutcome
{
    public required string SourceType { get; init; }
    public required string RootElementName { get; init; }
    public required bool IsRootCompleted { get; init; }
    public required IReadOnlyList<NormalizedBlacklistRecord> Records { get; init; }
    public required IReadOnlyList<(string RecordId, string ErrorMessage)> MalformedRecords { get; init; }
}

public static class SanctionsFeedParsers
{
    public static async Task<SanctionsFeedParseOutcome> ParseXmlStreamAsync(
        string sourceType,
        string sourceRefId,
        Stream xmlStream,
        int? maxRecords = null,
        CancellationToken ct = default)
    {
        var src = sourceType.Trim().ToUpperInvariant();
        var records = new List<NormalizedBlacklistRecord>();
        var malformed = new List<(string RecordId, string ErrorMessage)>();
        string? rootName = null;
        bool reachedEnd = false;

        var settings = new XmlReaderSettings
        {
            Async = true,
            IgnoreComments = true,
            IgnoreWhitespace = true,
            DtdProcessing = DtdProcessing.Ignore,
            CheckCharacters = false
        };

        using var reader = XmlReader.Create(xmlStream, settings);

        while (await reader.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();

            if (reader.NodeType == XmlNodeType.Element)
            {
                if (rootName is null)
                {
                    rootName = reader.LocalName;
                    ValidateExpectedRoot(src, rootName);
                    continue;
                }

                var local = reader.LocalName;
                if (IsEntryElement(src, local))
                {
                    try
                    {
                        using var subReader = reader.ReadSubtree();
                        await subReader.ReadAsync();
                        var element = (XElement)await XNode.ReadFromAsync(subReader, ct);
                        var parsed = ParseSingleElement(src, sourceRefId, local, element);

                        if (parsed is null || string.IsNullOrWhiteSpace(parsed.EntityExternalId) || string.IsNullOrWhiteSpace(parsed.FullName))
                        {
                            var rawId = parsed?.EntityExternalId ?? element.Attribute("logicalId")?.Value ?? element.Elements().FirstOrDefault(e => e.Name.LocalName.EndsWith("id", StringComparison.OrdinalIgnoreCase))?.Value ?? "UNKNOWN";
                            malformed.Add((rawId, $"Record in {src} feed is missing required identifier or full name."));
                        }
                        else
                        {
                            records.Add(BlacklistRecordComparer.CanonicalizeRecord(parsed));
                            if (maxRecords.HasValue && records.Count >= maxRecords.Value)
                            {
                                reachedEnd = true;
                                break;
                            }
                        }
                    }
                    catch (Exception ex) when (ex is not XmlException)
                    {
                        malformed.Add(("MALFORMED_NODE", ex.Message));
                    }
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement && rootName is not null &&
                     string.Equals(reader.LocalName, rootName, StringComparison.OrdinalIgnoreCase))
            {
                reachedEnd = true;
            }
        }

        if (rootName is null)
        {
            throw new XmlException($"The {src} XML stream did not contain a valid root element.");
        }

        if (!reachedEnd && !maxRecords.HasValue)
        {
            throw new XmlException($"The {src} XML stream ended prematurely before closing root element <{rootName}>.");
        }

        return new SanctionsFeedParseOutcome
        {
            SourceType = src,
            RootElementName = rootName,
            IsRootCompleted = reachedEnd,
            Records = records,
            MalformedRecords = malformed
        };
    }

    private static void ValidateExpectedRoot(string sourceType, string rootLocalName)
    {
        // Reject HTML error pages or captive portal responses masquerading as 200 OK
        if (string.Equals(rootLocalName, "html", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rootLocalName, "body", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rootLocalName, "Error", StringComparison.OrdinalIgnoreCase))
        {
            throw new XmlException($"Received an HTML or error document (<{rootLocalName}>) instead of a valid {sourceType} sanctions XML feed.");
        }
    }

    private static bool IsEntryElement(string sourceType, string localName)
    {
        return sourceType switch
        {
            "OFAC" => string.Equals(localName, "sdnEntry", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(localName, "DistinctParty", StringComparison.OrdinalIgnoreCase),
            "UN" => string.Equals(localName, "INDIVIDUAL", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(localName, "ENTITY", StringComparison.OrdinalIgnoreCase),
            "EU" => string.Equals(localName, "sanctionEntity", StringComparison.OrdinalIgnoreCase),
            "UK" => string.Equals(localName, "Designation", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(localName, "FinancialSanctionsTarget", StringComparison.OrdinalIgnoreCase),
            "WORLD_CHECK" => string.Equals(localName, "record", StringComparison.OrdinalIgnoreCase),
            _ => string.Equals(localName, "sdnEntry", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(localName, "INDIVIDUAL", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(localName, "ENTITY", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(localName, "sanctionEntity", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(localName, "Designation", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(localName, "record", StringComparison.OrdinalIgnoreCase)
        };
    }

    private static NormalizedBlacklistRecord? ParseSingleElement(
        string sourceType,
        string sourceRefId,
        string elementLocalName,
        XElement el)
    {
        return sourceType switch
        {
            "OFAC" => ParseOfacEntry(sourceRefId, el),
            "UN" => ParseUnEntry(sourceRefId, elementLocalName, el),
            "EU" => ParseEuEntry(sourceRefId, el),
            "UK" => ParseUkEntry(sourceRefId, el),
            "WORLD_CHECK" => ParseWorldCheckEntry(sourceRefId, el),
            _ => ParseOfacEntry(sourceRefId, el)
        };
    }

    private static NormalizedBlacklistRecord ParseOfacEntry(string sourceRefId, XElement el)
    {
        var uid = ChildVal(el, "uid") ?? el.Attribute("FixedRef")?.Value ?? string.Empty;
        var firstName = ChildVal(el, "firstName");
        var lastName = ChildVal(el, "lastName");
        var fullName = string.IsNullOrWhiteSpace(firstName)
            ? (lastName ?? string.Empty)
            : $"{firstName} {lastName}".Trim();

        var sdnType = ChildVal(el, "sdnType") ?? "Individual";
        var remarks = ChildVal(el, "remarks");

        var programs = el.Descendants()
            .Where(x => string.Equals(x.Name.LocalName, "program", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Value.Trim())
            .Where(x => x.Length > 0)
            .ToList();

        var aliases = new List<string>();
        foreach (var aka in el.Descendants().Where(x => string.Equals(x.Name.LocalName, "aka", StringComparison.OrdinalIgnoreCase)))
        {
            var aFirst = ChildVal(aka, "firstName");
            var aLast = ChildVal(aka, "lastName");
            var aName = string.IsNullOrWhiteSpace(aFirst) ? aLast : $"{aFirst} {aLast}".Trim();
            if (!string.IsNullOrWhiteSpace(aName))
            {
                aliases.Add(aName);
            }
        }

        var dobs = el.Descendants()
            .Where(x => string.Equals(x.Name.LocalName, "dateOfBirth", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Value.Trim())
            .Where(x => x.Length > 0)
            .ToList();

        var ids = new List<string>();
        foreach (var idNode in el.Descendants().Where(x => string.Equals(x.Name.LocalName, "id", StringComparison.OrdinalIgnoreCase)))
        {
            var idType = ChildVal(idNode, "idType");
            var idNum = ChildVal(idNode, "idNumber");
            var idCountry = ChildVal(idNode, "idCountry");
            if (!string.IsNullOrWhiteSpace(idNum))
            {
                var formatted = string.IsNullOrWhiteSpace(idType) ? idNum : $"{idType}: {idNum}";
                if (!string.IsNullOrWhiteSpace(idCountry))
                {
                    formatted += $" ({idCountry})";
                }
                ids.Add(formatted);
            }
        }

        var addresses = new List<string>();
        foreach (var addr in el.Descendants().Where(x => string.Equals(x.Name.LocalName, "address", StringComparison.OrdinalIgnoreCase)))
        {
            var parts = new[]
            {
                ChildVal(addr, "address1"),
                ChildVal(addr, "address2"),
                ChildVal(addr, "city"),
                ChildVal(addr, "stateOrProvince"),
                ChildVal(addr, "postalCode"),
                ChildVal(addr, "country")
            }.Where(p => !string.IsNullOrWhiteSpace(p));

            var combined = string.Join(", ", parts);
            if (!string.IsNullOrWhiteSpace(combined))
            {
                addresses.Add(combined);
            }
        }

        var nationalities = el.Descendants()
            .Where(x => string.Equals(x.Name.LocalName, "nationality", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(x.Name.LocalName, "citizenship", StringComparison.OrdinalIgnoreCase))
            .Select(x => ChildVal(x, "country") ?? x.Value.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToList();

        if (nationalities.Count == 0)
        {
            // Fallback to first address country if no explicit nationality node is present
            var addrCountry = el.Descendants()
                .Where(x => string.Equals(x.Name.LocalName, "address", StringComparison.OrdinalIgnoreCase))
                .Select(x => ChildVal(x, "country"))
                .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
            if (!string.IsNullOrWhiteSpace(addrCountry))
            {
                nationalities.Add(addrCountry);
            }
        }

        return new NormalizedBlacklistRecord
        {
            SourceType = "OFAC",
            SourceRefId = sourceRefId,
            SourceList = "OFAC_SDN",
            EntityExternalId = string.IsNullOrWhiteSpace(uid) ? string.Empty : $"OFAC-{uid}",
            EntityType = BlacklistRecordComparer.NormalizeEntityType(sdnType),
            FullName = fullName,
            Aliases = aliases,
            DatesOfBirth = dobs,
            Identifications = ids,
            Addresses = addresses,
            Nationalities = nationalities,
            Programs = programs.Count > 0 ? programs : ["OFAC_SDN"],
            Remarks = remarks
        };
    }

    private static NormalizedBlacklistRecord ParseUnEntry(string sourceRefId, string elementLocalName, XElement el)
    {
        var dataId = ChildVal(el, "DATAID") ?? string.Empty;
        var refNum = ChildVal(el, "REFERENCE_NUMBER");
        var first = ChildVal(el, "FIRST_NAME");
        var second = ChildVal(el, "SECOND_NAME");
        var third = ChildVal(el, "THIRD_NAME");
        var fourth = ChildVal(el, "FOURTH_NAME");

        var fullName = string.Join(" ", new[] { first, second, third, fourth }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
        var isEntity = string.Equals(elementLocalName, "ENTITY", StringComparison.OrdinalIgnoreCase);

        var aliases = el.Descendants()
            .Where(x => string.Equals(x.Name.LocalName, "INDIVIDUAL_ALIAS", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(x.Name.LocalName, "ENTITY_ALIAS", StringComparison.OrdinalIgnoreCase))
            .Select(x => ChildVal(x, "ALIAS_NAME"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToList();

        var dobs = new List<string>();
        foreach (var dobNode in el.Descendants().Where(x => string.Equals(x.Name.LocalName, "INDIVIDUAL_DATE_OF_BIRTH", StringComparison.OrdinalIgnoreCase)))
        {
            var dt = ChildVal(dobNode, "DATE") ?? ChildVal(dobNode, "YEAR") ?? ChildVal(dobNode, "FROM_YEAR");
            if (!string.IsNullOrWhiteSpace(dt))
            {
                dobs.Add(dt);
            }
        }

        var ids = new List<string>();
        if (!string.IsNullOrWhiteSpace(refNum))
        {
            ids.Add($"UN-REF: {refNum}");
        }
        foreach (var doc in el.Descendants().Where(x => string.Equals(x.Name.LocalName, "INDIVIDUAL_DOCUMENT", StringComparison.OrdinalIgnoreCase)))
        {
            var docType = ChildVal(doc, "TYPE_OF_DOCUMENT");
            var docNum = ChildVal(doc, "NUMBER");
            var docCountry = ChildVal(doc, "ISSUING_COUNTRY") ?? ChildVal(doc, "COUNTRY_OF_ISSUE");
            if (!string.IsNullOrWhiteSpace(docNum))
            {
                var formatted = string.IsNullOrWhiteSpace(docType) ? docNum : $"{docType}: {docNum}";
                if (!string.IsNullOrWhiteSpace(docCountry))
                {
                    formatted += $" ({docCountry})";
                }
                ids.Add(formatted);
            }
        }

        var addresses = new List<string>();
        foreach (var addr in el.Descendants().Where(x => string.Equals(x.Name.LocalName, "INDIVIDUAL_ADDRESS", StringComparison.OrdinalIgnoreCase) ||
                                                         string.Equals(x.Name.LocalName, "ENTITY_ADDRESS", StringComparison.OrdinalIgnoreCase)))
        {
            var parts = new[]
            {
                ChildVal(addr, "STREET"),
                ChildVal(addr, "CITY"),
                ChildVal(addr, "STATE_PROVINCE"),
                ChildVal(addr, "ZIP_CODE"),
                ChildVal(addr, "COUNTRY")
            }.Where(p => !string.IsNullOrWhiteSpace(p));

            var combined = string.Join(", ", parts);
            if (!string.IsNullOrWhiteSpace(combined))
            {
                addresses.Add(combined);
            }
        }

        var nationalities = el.Descendants()
            .Where(x => string.Equals(x.Name.LocalName, "NATIONALITY", StringComparison.OrdinalIgnoreCase))
            .SelectMany(x => x.Descendants().Where(v => string.Equals(v.Name.LocalName, "VALUE", StringComparison.OrdinalIgnoreCase)).Select(v => v.Value.Trim()))
            .Where(x => x.Length > 0)
            .ToList();

        var unListType = ChildVal(el, "UN_LIST_TYPE");
        var programs = !string.IsNullOrWhiteSpace(unListType) ? new List<string> { unListType } : ["UN_CONSOLIDATED"];
        var comments = ChildVal(el, "COMMENTS1");

        return new NormalizedBlacklistRecord
        {
            SourceType = "UN",
            SourceRefId = sourceRefId,
            SourceList = "UN_CONSOLIDATED",
            EntityExternalId = string.IsNullOrWhiteSpace(dataId) ? string.Empty : $"UN-{dataId}",
            EntityType = isEntity ? "CORPORATE" : "INDIVIDUAL",
            FullName = fullName,
            Aliases = aliases,
            DatesOfBirth = dobs,
            Identifications = ids,
            Addresses = addresses,
            Nationalities = nationalities,
            Programs = programs,
            Remarks = comments
        };
    }

    private static NormalizedBlacklistRecord ParseEuEntry(string sourceRefId, XElement el)
    {
        var logicalId = el.Attribute("logicalId")?.Value ?? ChildVal(el, "logicalId") ?? string.Empty;
        var euRef = el.Attribute("euReferenceNumber")?.Value;

        var subjNode = el.Elements().FirstOrDefault(x => string.Equals(x.Name.LocalName, "subjectType", StringComparison.OrdinalIgnoreCase));
        var subjCode = subjNode?.Attribute("code")?.Value ?? subjNode?.Attribute("classificationCode")?.Value ?? "person";
        var entityType = string.Equals(subjCode, "person", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(subjCode, "P", StringComparison.OrdinalIgnoreCase)
            ? "INDIVIDUAL"
            : "CORPORATE";

        var nameNodes = el.Elements().Where(x => string.Equals(x.Name.LocalName, "nameAlias", StringComparison.OrdinalIgnoreCase)).ToList();
        string fullName = string.Empty;
        var aliases = new List<string>();

        foreach (var n in nameNodes)
        {
            var whole = n.Attribute("wholeName")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(whole))
            {
                var fn = n.Attribute("firstName")?.Value?.Trim();
                var ln = n.Attribute("lastName")?.Value?.Trim();
                whole = $"{fn} {ln}".Trim();
            }

            if (!string.IsNullOrWhiteSpace(whole))
            {
                if (string.IsNullOrEmpty(fullName))
                {
                    fullName = whole;
                }
                else
                {
                    aliases.Add(whole);
                }
            }
        }

        var dobs = el.Elements()
            .Where(x => string.Equals(x.Name.LocalName, "birthdate", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Attribute("birthdate")?.Value ?? x.Attribute("year")?.Value ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        var ids = new List<string>();
        if (!string.IsNullOrWhiteSpace(euRef))
        {
            ids.Add($"EU-REF: {euRef}");
        }
        foreach (var idNode in el.Elements().Where(x => string.Equals(x.Name.LocalName, "identification", StringComparison.OrdinalIgnoreCase)))
        {
            var code = idNode.Attribute("identificationTypeCode")?.Value ?? idNode.Attribute("identificationTypeDescription")?.Value;
            var num = idNode.Attribute("number")?.Value ?? idNode.Attribute("latinNumber")?.Value;
            var country = idNode.Attribute("countryIso2Code")?.Value ?? idNode.Attribute("countryDescription")?.Value;
            if (!string.IsNullOrWhiteSpace(num))
            {
                var formatted = string.IsNullOrWhiteSpace(code) ? num : $"{code}: {num}";
                if (!string.IsNullOrWhiteSpace(country))
                {
                    formatted += $" ({country})";
                }
                ids.Add(formatted);
            }
        }

        var addresses = new List<string>();
        foreach (var addr in el.Elements().Where(x => string.Equals(x.Name.LocalName, "address", StringComparison.OrdinalIgnoreCase)))
        {
            var parts = new[]
            {
                addr.Attribute("street")?.Value,
                addr.Attribute("city")?.Value,
                addr.Attribute("region")?.Value,
                addr.Attribute("zipCode")?.Value,
                addr.Attribute("countryDescription")?.Value ?? addr.Attribute("countryIso2Code")?.Value
            }.Where(p => !string.IsNullOrWhiteSpace(p));

            var combined = string.Join(", ", parts);
            if (!string.IsNullOrWhiteSpace(combined))
            {
                addresses.Add(combined);
            }
        }

        var nationalities = el.Elements()
            .Where(x => string.Equals(x.Name.LocalName, "citizenship", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Attribute("countryIso2Code")?.Value ?? x.Attribute("countryDescription")?.Value ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        var programs = el.Descendants()
            .Where(x => string.Equals(x.Name.LocalName, "regulation", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Attribute("programme")?.Value ?? x.Attribute("numberTitle")?.Value ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        var remark = el.Elements()
            .Where(x => string.Equals(x.Name.LocalName, "remark", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Value.Trim())
            .FirstOrDefault(x => x.Length > 0);

        return new NormalizedBlacklistRecord
        {
            SourceType = "EU",
            SourceRefId = sourceRefId,
            SourceList = "EU_FSF",
            EntityExternalId = string.IsNullOrWhiteSpace(logicalId) ? string.Empty : $"EU-{logicalId}",
            EntityType = entityType,
            FullName = fullName,
            Aliases = aliases,
            DatesOfBirth = dobs,
            Identifications = ids,
            Addresses = addresses,
            Nationalities = nationalities,
            Programs = programs.Count > 0 ? programs : ["EU_FSF"],
            Remarks = remark
        };
    }

    private static NormalizedBlacklistRecord ParseUkEntry(string sourceRefId, XElement el)
    {
        var uniqueId = ChildVal(el, "UniqueID") ?? ChildVal(el, "OFSIGroupID") ?? ChildVal(el, "GroupID") ?? string.Empty;
        var ofsiGroupId = ChildVal(el, "OFSIGroupID");
        var indEntityShip = ChildVal(el, "IndividualEntityShip") ?? ChildVal(el, "GroupTypeDescription") ?? "Individual";

        string fullName = string.Empty;
        var aliases = new List<string>();

        var nameNodes = el.Descendants().Where(x => string.Equals(x.Name.LocalName, "Name", StringComparison.OrdinalIgnoreCase) && x.HasElements).ToList();
        foreach (var n in nameNodes)
        {
            var parts = new[]
            {
                ChildVal(n, "Name1"),
                ChildVal(n, "Name2"),
                ChildVal(n, "Name3"),
                ChildVal(n, "Name4"),
                ChildVal(n, "Name5"),
                ChildVal(n, "Name6")
            }.Where(p => !string.IsNullOrWhiteSpace(p));

            var candidate = string.Join(" ", parts).Trim();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var nameType = ChildVal(n, "NameType");
            if (string.IsNullOrEmpty(fullName) || string.Equals(nameType, "Primary Name", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(fullName) && !string.Equals(fullName, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    aliases.Add(fullName);
                }
                fullName = candidate;
            }
            else
            {
                aliases.Add(candidate);
            }
        }

        if (string.IsNullOrEmpty(fullName))
        {
            // Flat UK format fallback
            var flatParts = new[]
            {
                ChildVal(el, "Name1"),
                ChildVal(el, "Name2"),
                ChildVal(el, "Name3"),
                ChildVal(el, "Name4"),
                ChildVal(el, "Name5"),
                ChildVal(el, "Name6")
            }.Where(p => !string.IsNullOrWhiteSpace(p));
            fullName = string.Join(" ", flatParts).Trim();
        }

        var dobs = el.Descendants()
            .Where(x => string.Equals(x.Name.LocalName, "DOB", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Value.Trim())
            .Where(x => x.Length > 0)
            .ToList();

        var ids = new List<string>();
        if (!string.IsNullOrWhiteSpace(ofsiGroupId))
        {
            ids.Add($"OFSI-GROUP: {ofsiGroupId}");
        }
        foreach (var p in el.Descendants().Where(x => string.Equals(x.Name.LocalName, "Passport", StringComparison.OrdinalIgnoreCase) ||
                                                      string.Equals(x.Name.LocalName, "NationalIdentifier", StringComparison.OrdinalIgnoreCase)))
        {
            var num = ChildVal(p, "PassportNumber") ?? ChildVal(p, "NationalIdentifierNumber");
            var country = ChildVal(p, "PassportAdditionalInformation") ?? ChildVal(p, "NationalIdentifierAdditionalInformation");
            if (!string.IsNullOrWhiteSpace(num))
            {
                ids.Add(string.IsNullOrWhiteSpace(country) ? $"{p.Name.LocalName}: {num}" : $"{p.Name.LocalName}: {num} ({country})");
            }
        }

        var addresses = new List<string>();
        foreach (var addr in el.Descendants().Where(x => string.Equals(x.Name.LocalName, "Address", StringComparison.OrdinalIgnoreCase) && x.HasElements))
        {
            var parts = new[]
            {
                ChildVal(addr, "AddressLine1"),
                ChildVal(addr, "AddressLine2"),
                ChildVal(addr, "AddressLine3"),
                ChildVal(addr, "AddressLine4"),
                ChildVal(addr, "AddressLine5"),
                ChildVal(addr, "AddressLine6"),
                ChildVal(addr, "AddressPostCode"),
                ChildVal(addr, "AddressCountry")
            }.Where(p => !string.IsNullOrWhiteSpace(p));

            var combined = string.Join(", ", parts);
            if (!string.IsNullOrWhiteSpace(combined))
            {
                addresses.Add(combined);
            }
        }

        var nationalities = el.Descendants()
            .Where(x => string.Equals(x.Name.LocalName, "Nationality", StringComparison.OrdinalIgnoreCase) && !x.HasElements)
            .Select(x => x.Value.Trim())
            .Where(x => x.Length > 0)
            .ToList();

        var regime = ChildVal(el, "RegimeName");
        var programs = !string.IsNullOrWhiteSpace(regime) ? new List<string> { regime } : ["UK_SANCTIONS"];
        var otherInfo = ChildVal(el, "OtherInformation") ?? ChildVal(el, "UKStatementOfReasons");

        return new NormalizedBlacklistRecord
        {
            SourceType = "UK",
            SourceRefId = sourceRefId,
            SourceList = "UK_SANCTIONS",
            EntityExternalId = string.IsNullOrWhiteSpace(uniqueId) ? string.Empty : $"UK-{uniqueId}",
            EntityType = BlacklistRecordComparer.NormalizeEntityType(indEntityShip),
            FullName = fullName,
            Aliases = aliases,
            DatesOfBirth = dobs,
            Identifications = ids,
            Addresses = addresses,
            Nationalities = nationalities,
            Programs = programs,
            Remarks = otherInfo
        };
    }

    private static NormalizedBlacklistRecord? ParseWorldCheckEntry(string sourceRefId, XElement el)
    {
        var uid = el.Attribute("uid")?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(uid))
        {
            return null;
        }

        var extId = $"WC-{uid}";
        var category = el.Attribute("category")?.Value?.Trim() ?? "SANCTION";
        var subCat = el.Attribute("sub-category")?.Value?.Trim() ?? string.Empty;

        var person = el.Element("person");
        var ei = person?.Attribute("e-i")?.Value?.Trim()?.ToUpperInvariant() ?? "U";
        var entityType = ei == "E" ? "CORPORATE" : "INDIVIDUAL";
        var position = person?.Element("position")?.Value?.Trim();

        var names = person?.Element("names");
        var firstName = names?.Element("first_name")?.Value?.Trim();
        var lastName = names?.Element("last_name")?.Value?.Trim();
        var fullName = !string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName)
            ? $"{firstName} {lastName}"
            : (!string.IsNullOrWhiteSpace(lastName) ? lastName : (firstName ?? string.Empty));

        var aliases = new List<string>();
        if (names?.Element("aliases") is { } aliasesEl)
        {
            foreach (var a in aliasesEl.Elements("alias"))
            {
                var val = a.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(val) && !val.Equals("null", StringComparison.OrdinalIgnoreCase))
                {
                    aliases.Add(val);
                }
            }
        }

        var dobs = new List<string>();
        if (person?.Element("agedata") is { } ageData)
        {
            var rawDob = ageData.Element("dob")?.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(rawDob) && !rawDob.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                dobs.Add(rawDob);
            }
            if (ageData.Element("dobs") is { } dobsEl)
            {
                foreach (var d in dobsEl.Elements("dob"))
                {
                    var dt = d.Value?.Trim();
                    if (!string.IsNullOrWhiteSpace(dt) && !dt.Equals("null", StringComparison.OrdinalIgnoreCase) && !dobs.Contains(dt))
                    {
                        dobs.Add(dt);
                    }
                }
            }
        }

        var ids = new List<string>();
        var addresses = new List<string>();
        var nationalities = new List<string>();
        string? furtherInfo = null;

        if (el.Element("details") is { } details)
        {
            furtherInfo = details.Element("further_information")?.Value?.Trim();

            if (details.Element("passports") is { } passportsEl)
            {
                foreach (var p in passportsEl.Elements("passport"))
                {
                    var val = p.Value?.Trim();
                    var country = p.Attribute("country")?.Value?.Trim();
                    if (!string.IsNullOrWhiteSpace(val) && !val.Equals("null", StringComparison.OrdinalIgnoreCase))
                    {
                        ids.Add(string.IsNullOrWhiteSpace(country) ? $"Passport {val}" : $"Passport {val} ({country})");
                    }
                }
            }

            if (details.Element("id_numbers") is { } idsEl)
            {
                foreach (var id in idsEl.Elements("id"))
                {
                    var val = id.Value?.Trim();
                    var itype = id.Attribute("type")?.Value?.Trim();
                    var loc = id.Attribute("loc")?.Value?.Trim();
                    if (!string.IsNullOrWhiteSpace(val) && !val.Equals("null", StringComparison.OrdinalIgnoreCase))
                    {
                        var prefix = !string.IsNullOrWhiteSpace(itype) ? $"{itype}: " : "ID: ";
                        var suffix = !string.IsNullOrWhiteSpace(loc) ? $" ({loc})" : "";
                        ids.Add($"{prefix}{val}{suffix}");
                    }
                }
            }

            if (details.Element("locations") is { } locsEl)
            {
                foreach (var loc in locsEl.Elements("location"))
                {
                    var city = loc.Attribute("city")?.Value?.Trim();
                    var state = loc.Attribute("state")?.Value?.Trim();
                    var country = loc.Attribute("country")?.Value?.Trim();
                    var locStr = string.Join(", ", new[] { city, state, country }.Where(x => !string.IsNullOrWhiteSpace(x)));
                    if (!string.IsNullOrWhiteSpace(locStr))
                    {
                        addresses.Add(locStr);
                    }
                }
            }

            if (details.Element("citizenships") is { } citsEl)
            {
                foreach (var c in citsEl.Elements("citizenship"))
                {
                    var val = c.Value?.Trim();
                    if (!string.IsNullOrWhiteSpace(val) && !val.Equals("null", StringComparison.OrdinalIgnoreCase)) nationalities.Add(val);
                }
            }
            if (details.Element("countries") is { } countriesEl)
            {
                foreach (var c in countriesEl.Elements("country"))
                {
                    var val = c.Value?.Trim();
                    if (!string.IsNullOrWhiteSpace(val) && !val.Equals("null", StringComparison.OrdinalIgnoreCase) && !nationalities.Contains(val))
                    {
                        nationalities.Add(val);
                    }
                }
            }
        }

        var programs = !string.IsNullOrWhiteSpace(subCat) ? new List<string> { subCat } : ["WORLD_CHECK"];
        var remarks = string.Join(" | ", new[] { position, furtherInfo }.Where(x => !string.IsNullOrWhiteSpace(x)));

        return new NormalizedBlacklistRecord
        {
            SourceType = "WORLD_CHECK",
            SourceRefId = sourceRefId,
            SourceList = "WORLD_CHECK",
            EntityExternalId = extId,
            EntityType = entityType,
            FullName = fullName,
            Category = category,
            Aliases = aliases,
            DatesOfBirth = dobs,
            Identifications = ids,
            Addresses = addresses,
            Nationalities = nationalities,
            Programs = programs,
            Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks
        };
    }

    private static string? ChildVal(XElement parent, string localName)
    {
        var child = parent.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));
        var val = child?.Value?.Trim();
        return string.IsNullOrWhiteSpace(val) ? null : val;
    }
}
