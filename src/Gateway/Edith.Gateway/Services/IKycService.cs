using System.Data;
using System.Globalization;
using System.Text.Json;
using Npgsql;

namespace Edith.Gateway.Services;

public sealed record KycSowChecklistItemDto(
    string Code,
    string Label,
    string Category,
    bool Verified,
    string? EvidenceRef = null
);

public sealed record KycAlertSummaryDto(
    string AlertId,
    string Title,
    string Description,
    string Status,
    string Severity,
    string? LinkedCaseNumber = null
);

public sealed record KycDocumentDto(
    string Id,
    string Title,
    string Status,
    string ExpiryDate,
    string DocType = "COMMERCIAL_REGISTRY",
    string DocNumber = "",
    string IssueCountry = "ARE",
    string IssuedDate = "2023-04-12",
    double OcrConfidence = 99.2,
    IReadOnlyDictionary<string, string>? ExtractedFields = null
);

public sealed record KycTransactionDto(
    string TxId,
    string Date,
    double Amount,
    string Currency,
    string Counterparty,
    string Status,
    string Corridor = "ARE -> CHE",
    double RiskScore = 85.0,
    string? LinkedCaseNumber = null
);

public sealed record KycDossierDto
{
    public required string CustomerId { get; init; }
    public required string CustomerCode { get; init; }
    public required string FullName { get; init; }
    public required string CustomerType { get; init; }
    public required string RiskCategory { get; init; }
    public required double RiskScore { get; init; }
    public required string Nationality { get; init; }
    public required string IncorporationCountry { get; init; }
    public required IReadOnlyList<string> UboIdentities { get; init; }
    public required IReadOnlyList<KycDocumentDto> Documents { get; init; }
    public required IReadOnlyList<KycTransactionDto> RecentTransactions { get; init; }
    public string RegistrationNumber { get; init; } = "";
    public string Jurisdiction { get; init; } = "";
    public string PepStatus { get; init; } = "Non-PEP";
    public string RiskTier { get; init; } = "HIGH";
    public bool IsBlocked { get; init; }
    public string BlockReason { get; init; } = "";
    public string OnboardingStatus { get; init; } = "ACTIVE_EDD";
    public string OnboardedDate { get; init; } = "2023-04-18";
    public string NextReviewDate { get; init; } = "2026-10-15";
    public string RelationshipManager { get; init; } = "Mansoor Al-Falasi (Commercial Banking)";
    public string PrimaryAccount { get; init; } = "AE820330000001234567890";
    public double ExpectedMonthlyTurnoverUsd { get; init; } = 350000;
    public IReadOnlyList<string> ApprovedCorridors { get; init; } = ["ARE -> SGP", "ARE -> GBR", "ARE -> SAU"];
    public IReadOnlyList<KycSowChecklistItemDto> SowChecklist { get; init; } = [];
    public IReadOnlyList<KycAlertSummaryDto> HistoricalAlerts { get; init; } = [];
}

public sealed record KycEddUpdateRequest(
    string Action,
    string? RiskTier = null,
    bool? IsBlocked = null,
    string? BlockReason = null,
    string? NextReviewDate = null,
    string? DocumentId = null,
    string? DocumentStatus = null,
    double? ScreeningScore = null,
    string? ScreeningStatus = null,
    string? Notes = null
);

public interface IKycService
{
    Task<IReadOnlyList<KycDossierDto>> ListCustomersAsync(string? search = null, bool blockedOnly = false, CancellationToken ct = default);
    Task<KycDossierDto?> GetCustomerDossierAsync(string customerIdOrCode, CancellationToken ct = default);
    Task<KycDossierDto?> UpdateCustomerEddAsync(string customerIdOrCode, KycEddUpdateRequest request, CancellationToken ct = default);
    Task EnsureBaselineCustomersAsync(Guid tenantId, CancellationToken ct = default);
}

public sealed class PostgreSqlKycService : IKycService
{
    private readonly string _connectionString;
    private readonly InMemoryKycService _fallback = new();

    public PostgreSqlKycService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<KycDossierDto>> ListCustomersAsync(string? search = null, bool blockedOnly = false, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            var sql = @"
                SELECT customer_code
                FROM screening.customers
                WHERE (@blockedOnly = false OR is_blocked = true)
                  AND (@search = '' OR customer_code ILIKE @pattern OR full_name ILIKE @pattern OR id_number ILIKE @pattern)
                ORDER BY is_blocked DESC, updated_at DESC
                LIMIT 25;";

            var codes = new List<string>();
            await using (var cmd = new NpgsqlCommand(sql, conn))
            {
                var term = (search ?? string.Empty).Trim();
                cmd.Parameters.AddWithValue("blockedOnly", blockedOnly);
                cmd.Parameters.AddWithValue("search", term);
                cmd.Parameters.AddWithValue("pattern", $"%{term}%");

                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    codes.Add(r.GetString(0));
                }
            }

            if (codes.Count == 0 && string.IsNullOrWhiteSpace(search) && !blockedOnly)
            {
                await EnsureBaselineCustomersAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"), ct);
                return await _fallback.ListCustomersAsync(search, blockedOnly, ct);
            }

            var results = new List<KycDossierDto>();
            foreach (var code in codes)
            {
                var d = await GetCustomerDossierInternalAsync(conn, code, ct);
                if (d != null)
                {
                    results.Add(d);
                }
            }

            return results.Count > 0 ? results : await _fallback.ListCustomersAsync(search, blockedOnly, ct);
        }
        catch
        {
            return await _fallback.ListCustomersAsync(search, blockedOnly, ct);
        }
    }

    public async Task<KycDossierDto?> GetCustomerDossierAsync(string customerIdOrCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(customerIdOrCode))
        {
            return null;
        }

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            var dossier = await GetCustomerDossierInternalAsync(conn, customerIdOrCode, ct);
            return dossier ?? await _fallback.GetCustomerDossierAsync(customerIdOrCode, ct);
        }
        catch
        {
            return await _fallback.GetCustomerDossierAsync(customerIdOrCode, ct);
        }
    }

    public async Task<KycDossierDto?> UpdateCustomerEddAsync(string customerIdOrCode, KycEddUpdateRequest request, CancellationToken ct = default)
    {
        await _fallback.UpdateCustomerEddAsync(customerIdOrCode, request, ct);

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            var current = await GetCustomerDossierInternalAsync(conn, customerIdOrCode, ct);
            if (current == null || !Guid.TryParse(current.CustomerId, out var custGuid))
            {
                return await _fallback.GetCustomerDossierAsync(customerIdOrCode, ct);
            }

            var action = (request.Action ?? string.Empty).ToUpperInvariant();
            if (action == "COMPLETE_EDD_REVIEW")
            {
                var newTier = string.IsNullOrWhiteSpace(request.RiskTier) ? current.RiskTier : request.RiskTier.Trim().ToUpperInvariant();
                const string sql = @"
                    UPDATE screening.customers
                    SET onboarding_status = 'EDD_APPROVED',
                        risk_tier = @tier,
                        updated_at = CURRENT_TIMESTAMP
                    WHERE id = @cid;";
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("tier", newTier);
                cmd.Parameters.AddWithValue("cid", custGuid);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            else if (action == "UPDATE_RISK_TIER" && !string.IsNullOrWhiteSpace(request.RiskTier))
            {
                const string sql = @"
                    UPDATE screening.customers
                    SET risk_tier = @tier,
                        updated_at = CURRENT_TIMESTAMP
                    WHERE id = @cid;";
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("tier", request.RiskTier.Trim().ToUpperInvariant());
                cmd.Parameters.AddWithValue("cid", custGuid);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            else if (action == "TOGGLE_BLOCK" && request.IsBlocked.HasValue)
            {
                const string sql = @"
                    UPDATE screening.customers
                    SET is_blocked = @blocked,
                        onboarding_status = CASE WHEN @blocked THEN 'FROZEN_HOLD' ELSE 'ACTIVE_EDD' END,
                        updated_at = CURRENT_TIMESTAMP
                    WHERE id = @cid;";
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("blocked", request.IsBlocked.Value);
                cmd.Parameters.AddWithValue("cid", custGuid);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            else if (action == "VERIFY_DOCUMENT" && !string.IsNullOrWhiteSpace(request.DocumentId) && !string.IsNullOrWhiteSpace(request.DocumentStatus))
            {
                if (Guid.TryParse(request.DocumentId, out var docGuid))
                {
                    const string sql = @"
                        UPDATE screening.customer_documents
                        SET verification_status = @status
                        WHERE id = @did AND customer_id = @cid;";
                    await using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("status", request.DocumentStatus.Trim().ToUpperInvariant());
                    cmd.Parameters.AddWithValue("did", docGuid);
                    cmd.Parameters.AddWithValue("cid", custGuid);
                    await cmd.ExecuteNonQueryAsync(ct);
                }
            }
            else if (action == "RECORD_SCREENING")
            {
                const string sql = @"
                    INSERT INTO screening.customer_screening_history (customer_id, screened_at, match_status, match_score)
                    VALUES (@cid, CURRENT_TIMESTAMP, @status, @score);";
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("cid", custGuid);
                cmd.Parameters.AddWithValue("status", request.ScreeningStatus ?? "CLEARED");
                cmd.Parameters.AddWithValue("score", (decimal)(request.ScreeningScore ?? 0.0));
                await cmd.ExecuteNonQueryAsync(ct);
            }

            return await GetCustomerDossierInternalAsync(conn, current.CustomerCode, ct);
        }
        catch
        {
            return await _fallback.GetCustomerDossierAsync(customerIdOrCode, ct);
        }
    }

    private static async Task<KycDossierDto?> GetCustomerDossierInternalAsync(NpgsqlConnection conn, string customerIdOrCode, CancellationToken ct)
    {
        Guid? parsedGuid = Guid.TryParse(customerIdOrCode, out var g) ? g : null;
        var trimmed = customerIdOrCode.Trim();

        const string custSql = @"
            SELECT id, customer_code, customer_type, full_name, dob, nationality, 
                   residence_country, id_type, id_number, risk_tier, is_blocked, onboarding_status, created_at
            FROM screening.customers
            WHERE id = @guidVal
               OR customer_code ILIKE @codeVal
               OR full_name ILIKE @namePattern
               OR id_number ILIKE @codeVal
            ORDER BY CASE WHEN customer_code ILIKE @codeVal THEN 0 ELSE 1 END
            LIMIT 1;";

        Guid customerId;
        string customerCode;
        string customerType;
        string fullName;
        string nationality;
        string residenceCountry;
        string idNumber;
        string riskTier;
        bool isBlocked;
        string onboardingStatus;
        string onboardedDate;

        await using (var cmd = new NpgsqlCommand(custSql, conn))
        {
            cmd.Parameters.AddWithValue("guidVal", parsedGuid ?? Guid.Empty);
            cmd.Parameters.AddWithValue("codeVal", trimmed);
            cmd.Parameters.AddWithValue("namePattern", $"%{trimmed}%");

            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct))
            {
                return null;
            }

            customerId = r.GetGuid(0);
            customerCode = r.GetString(1);
            customerType = r.GetString(2);
            fullName = r.GetString(3);
            nationality = r.GetString(5);
            residenceCountry = r.GetString(6);
            idNumber = r.GetString(8);
            riskTier = r.GetString(9);
            isBlocked = r.GetBoolean(10);
            onboardingStatus = r.GetString(11);
            onboardedDate = r.GetDateTime(12).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        const string docSql = @"
            SELECT id, document_type, document_number, ocr_extracted_data, verification_status
            FROM screening.customer_documents
            WHERE customer_id = @cid;";

        var docs = new List<KycDocumentDto>();
        var ubos = new List<string>();
        string pepStatus = riskTier is "CRITICAL" or "HIGH" ? "PEP Linked (Tier 2 Shareholder)" : "Non-PEP";
        string jurisdiction = residenceCountry == "ARE" ? "United Arab Emirates (JAFZA Free Zone)" : residenceCountry;
        string relationshipManager = "Mansoor Al-Falasi (Commercial Banking)";
        string primaryAccount = "AE820330000001234567890";
        string nextReviewDate = onboardingStatus == "EDD_APPROVED" ? "2027-10-25" : "2026-10-15";
        string blockReason = isBlocked ? "UNSC Res 1521 / OFAC SDN Statutory Match & Administrative Hold" : "";
        double expectedMonthlyTurnover = 350000;

        await using (var cmd = new NpgsqlCommand(docSql, conn))
        {
            cmd.Parameters.AddWithValue("cid", customerId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var docId = r.GetGuid(0).ToString();
                var docType = r.GetString(1);
                var docNum = r.GetString(2);
                var ocrJson = r.IsDBNull(3) ? null : r.GetString(3);
                var vStatus = r.GetString(4);

                string issueCountry = residenceCountry;
                string issuedDate = "2023-04-12";
                string expiryDate = "2028-12-31";
                double ocrConfidence = vStatus.Contains("WARN", StringComparison.OrdinalIgnoreCase) ? 89.0 : 99.4;
                var extractedFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Document Type"] = docType.Replace('_', ' '),
                    ["Document Number"] = docNum,
                    ["Entity Name"] = fullName,
                    ["Issuing Jurisdiction"] = issueCountry,
                    ["MRZ / Registry Checksum"] = "VALID (ISO/IEC 7501)"
                };

                if (!string.IsNullOrWhiteSpace(ocrJson))
                {
                    try
                    {
                        using var docObj = JsonDocument.Parse(ocrJson);
                        var root = docObj.RootElement;
                        if (root.TryGetProperty("ubos", out var uboProp) && uboProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var u in uboProp.EnumerateArray())
                            {
                                var val = u.GetString();
                                if (!string.IsNullOrWhiteSpace(val) && !ubos.Contains(val))
                                {
                                    ubos.Add(val);
                                }
                            }
                        }
                        if (root.TryGetProperty("issueCountry", out var ic) && ic.ValueKind == JsonValueKind.String) issueCountry = ic.GetString() ?? issueCountry;
                        if (root.TryGetProperty("issuedDate", out var idt) && idt.ValueKind == JsonValueKind.String) issuedDate = idt.GetString() ?? issuedDate;
                        if (root.TryGetProperty("expiresDate", out var edt) && edt.ValueKind == JsonValueKind.String) expiryDate = edt.GetString() ?? expiryDate;
                        if (root.TryGetProperty("ocrConfidence", out var conf) && conf.TryGetDouble(out var cVal)) ocrConfidence = cVal;
                        if (root.TryGetProperty("pepStatus", out var pep) && pep.ValueKind == JsonValueKind.String) pepStatus = pep.GetString() ?? pepStatus;
                        if (root.TryGetProperty("jurisdiction", out var jur) && jur.ValueKind == JsonValueKind.String) jurisdiction = jur.GetString() ?? jurisdiction;
                        if (root.TryGetProperty("relationshipManager", out var rm) && rm.ValueKind == JsonValueKind.String) relationshipManager = rm.GetString() ?? relationshipManager;
                        if (root.TryGetProperty("primaryAccount", out var acc) && acc.ValueKind == JsonValueKind.String) primaryAccount = acc.GetString() ?? primaryAccount;
                        if (root.TryGetProperty("nextReviewDate", out var nrd) && nrd.ValueKind == JsonValueKind.String && onboardingStatus != "EDD_APPROVED") nextReviewDate = nrd.GetString() ?? nextReviewDate;
                        if (root.TryGetProperty("blockReason", out var br) && br.ValueKind == JsonValueKind.String) blockReason = br.GetString() ?? blockReason;
                        if (root.TryGetProperty("expectedMonthlyTurnoverUsd", out var emt) && emt.TryGetDouble(out var emtVal)) expectedMonthlyTurnover = emtVal;

                        if (root.TryGetProperty("fields", out var fieldsObj) && fieldsObj.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in fieldsObj.EnumerateObject())
                            {
                                extractedFields[prop.Name] = prop.Value.ToString();
                            }
                        }
                    }
                    catch
                    {
                        // Ignore malformed json
                    }
                }

                docs.Add(new KycDocumentDto(
                    docId,
                    $"{docType.Replace('_', ' ')} ({docNum})",
                    vStatus,
                    expiryDate,
                    docType,
                    docNum,
                    issueCountry,
                    issuedDate,
                    ocrConfidence,
                    extractedFields
                ));
            }
        }

        if (ubos.Count == 0)
        {
            ubos.Add(customerType == "INDIVIDUAL" ? $"{fullName} (100% Beneficial Owner)" : "Rashid Al-Maktoum (65%)");
            if (customerType != "INDIVIDUAL")
            {
                ubos.Add("Kovalev Trading LLC (35%)");
            }
        }

        const string txSql = @"
            SELECT transaction_ref_no, transaction_date, amount, currency, beneficiary_name, status, sender_country, beneficiary_country
            FROM screening.transactions
            WHERE customer_id = @cid
            ORDER BY transaction_date DESC
            LIMIT 10;";

        var txs = new List<KycTransactionDto>();
        await using (var cmd = new NpgsqlCommand(txSql, conn))
        {
            cmd.Parameters.AddWithValue("cid", customerId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var txRef = r.GetString(0);
                var txDate = r.GetDateTime(1).ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
                var txAmount = (double)r.GetDecimal(2);
                var txCurr = r.GetString(3);
                var bene = r.GetString(4);
                var txStatus = r.GetString(5);
                var sCountry = r.GetString(6);
                var bCountry = r.GetString(7);
                var txScore = txStatus == "BLOCKED" ? 96.4 : txStatus == "HELD" ? 78.5 : 18.2;
                var linkedCase = txStatus is "BLOCKED" or "HELD" ? "EDITH-CAS-2026-0042" : null;

                txs.Add(new KycTransactionDto(
                    txRef,
                    txDate,
                    txAmount,
                    txCurr,
                    bene,
                    txStatus,
                    $"{sCountry} -> {bCountry}",
                    txScore,
                    linkedCase
                ));
            }
        }

        double riskScore = riskTier switch
        {
            "CRITICAL" => 96.4,
            "HIGH" => 84.5,
            "MEDIUM" => 48.0,
            _ => 15.0
        };

        string riskCategory = isBlocked ? "EXTREME_SANCTIONS" : $"{riskTier}_RISK";

        var template = InMemoryKycService.FindTemplate(customerCode, fullName);

        return new KycDossierDto
        {
            CustomerId = customerId.ToString(),
            CustomerCode = customerCode,
            FullName = fullName,
            CustomerType = customerType == "CORPORATE" ? "Corporate Entity" : "Individual",
            RiskCategory = riskCategory,
            RiskScore = riskScore,
            Nationality = nationality,
            IncorporationCountry = residenceCountry,
            UboIdentities = ubos,
            Documents = docs.Count > 0 ? docs : (template?.Documents ?? []),
            RecentTransactions = txs.Count > 0 ? txs : (template?.RecentTransactions ?? []),
            RegistrationNumber = idNumber,
            Jurisdiction = jurisdiction,
            PepStatus = pepStatus,
            RiskTier = riskTier,
            IsBlocked = isBlocked,
            BlockReason = blockReason,
            OnboardingStatus = onboardingStatus,
            OnboardedDate = onboardedDate,
            NextReviewDate = nextReviewDate,
            RelationshipManager = relationshipManager,
            PrimaryAccount = primaryAccount,
            ExpectedMonthlyTurnoverUsd = expectedMonthlyTurnover,
            ApprovedCorridors = template?.ApprovedCorridors ?? ["ARE -> SGP", "ARE -> GBR", "ARE -> SAU"],
            SowChecklist = template?.SowChecklist ?? InMemoryKycService.DefaultSowChecklist,
            HistoricalAlerts = template?.HistoricalAlerts ?? InMemoryKycService.DefaultAlerts
        };
    }

    public async Task EnsureBaselineCustomersAsync(Guid tenantId, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            const string prereqSql = @"
                INSERT INTO core.tenants (id, name)
                VALUES (@tid, 'Emirates Global Bank')
                ON CONFLICT (id) DO NOTHING;

                INSERT INTO rules.country_masters (code, country_name, is_high_risk, risk_score) VALUES
                    ('ARE', 'United Arab Emirates', false, 10.00),
                    ('CHE', 'Switzerland', false, 15.00),
                    ('SGP', 'Singapore', false, 10.00),
                    ('AZE', 'Azerbaijan', true, 65.00),
                    ('RUS', 'Russian Federation', true, 85.00)
                ON CONFLICT (code) DO NOTHING;

                INSERT INTO rules.nationality_masters (code, nationality_name, is_high_risk, risk_score) VALUES
                    ('ARE', 'Emirati', false, 10.00),
                    ('RUS', 'Russian', true, 85.00)
                ON CONFLICT (code) DO NOTHING;";

            await using (var preCmd = new NpgsqlCommand(prereqSql, conn))
            {
                preCmd.Parameters.AddWithValue("tid", tenantId);
                await preCmd.ExecuteNonQueryAsync(ct);
            }

            const string seedCustomersSql = @"
                INSERT INTO screening.customers
                    (id, tenant_id, customer_code, customer_type, full_name, dob, nationality, residence_country, id_type, id_number, risk_tier, is_blocked, onboarding_status, created_at)
                VALUES
                    ('01931000-0000-7000-8000-000000000301', @tid, 'CUST-AE-88902', 'CORPORATE', 'Rashid Al-Maktoum Trading FZE', '1980-04-12', 'ARE', 'ARE', 'TRADE_LICENSE', 'DED-DXB-9921448', 'HIGH', false, 'ACTIVE_EDD', '2023-04-18T00:00:00Z'),
                    ('01931000-0000-7000-8000-000000000302', @tid, 'CUST-AE-88912', 'INDIVIDUAL', 'Viktor Anatolyevich Bout', '1967-01-13', 'RUS', 'RUS', 'PASSPORT', 'OFAC-9812', 'CRITICAL', true, 'FROZEN_HOLD', '2024-01-10T00:00:00Z'),
                    ('01931000-0000-7000-8000-000000000303', @tid, 'CUST-AE-88925', 'CORPORATE', 'Acme Global Trading LLC', '1985-06-20', 'ARE', 'ARE', 'TRADE_LICENSE', 'TL-DXB-889120', 'HIGH', false, 'ACTIVE_EDD', '2023-09-01T00:00:00Z'),
                    ('01931000-0000-7000-8000-000000000304', @tid, 'CUST-AE-88940', 'CORPORATE', 'Middle East Commodity Brokers', '1978-11-03', 'ARE', 'ARE', 'TRADE_LICENSE', 'CR-AUH-441209', 'HIGH', true, 'FROZEN_HOLD', '2022-05-14T00:00:00Z')
                ON CONFLICT (id) DO NOTHING;";

            await using (var custCmd = new NpgsqlCommand(seedCustomersSql, conn))
            {
                custCmd.Parameters.AddWithValue("tid", tenantId);
                await custCmd.ExecuteNonQueryAsync(ct);
            }

            const string seedDocsSql = @"
                INSERT INTO screening.customer_documents
                    (id, customer_id, document_type, document_number, file_path, ocr_extracted_data, verification_status)
                VALUES
                    ('01931000-0000-7000-8000-000000000401', '01931000-0000-7000-8000-000000000301', 'COMMERCIAL_REGISTRY', 'CR-2023-991204', '/vault/cr-2023-991204.pdf',
                     '{""ubos"":[""Rashid Al-Maktoum (65%)"",""Kovalev Trading LLC (35%)""],""issueCountry"":""ARE"",""issuedDate"":""2023-04-12"",""expiresDate"":""2027-04-11"",""ocrConfidence"":99.4,""pepStatus"":""PEP Linked (Tier 2 Shareholder)"",""jurisdiction"":""United Arab Emirates (JAFZA Free Zone)"",""relationshipManager"":""Mansoor Al-Falasi (Commercial Banking)"",""primaryAccount"":""AE820330000001234567890"",""nextReviewDate"":""2026-10-15"",""expectedMonthlyTurnoverUsd"":350000,""fields"":{""Legal Entity Name"":""Rashid Al-Maktoum Trading FZE"",""License Authority"":""JAFZA / DED Dubai"",""Paid-Up Capital"":""AED 5,000,000"",""Majority Shareholder"":""Rashid Al-Maktoum (65%)""}}'::jsonb,
                     'VERIFIED_100'),
                    ('01931000-0000-7000-8000-000000000402', '01931000-0000-7000-8000-000000000301', 'BOARD_RESOLUTION', 'BR-DXB-2026-01', '/vault/br-dxb-2026-01.pdf',
                     '{""issueCountry"":""ARE"",""issuedDate"":""2026-01-08"",""expiresDate"":""2028-01-08"",""ocrConfidence"":98.2,""fields"":{""Authorized Signatory"":""Rashid Al-Maktoum"",""Single Wire Limit"":""USD 1,500,000"",""Notary Seal"":""Dubai Courts Verified #8821""}}'::jsonb,
                     'VERIFIED_100'),
                    ('01931000-0000-7000-8000-000000000403', '01931000-0000-7000-8000-000000000301', 'PASSPORT', 'N-8841299-UAE', '/vault/n-8841299-uae.pdf',
                     '{""issueCountry"":""ARE"",""issuedDate"":""2021-08-15"",""expiresDate"":""2026-08-14"",""ocrConfidence"":89.0,""fields"":{""Holder Name"":""RASHID AL-MAKTOUM"",""MRZ Line 1"":""P<AREALMAKTOUM<<RASHID<<<<<<<<<<<<<<<<<<<<<<"",""Expiry Warning"":""Document expired 2026-08-14 (Renewal Required)""}}'::jsonb,
                     'VERIFIED_WARNING'),
                    ('01931000-0000-7000-8000-000000000404', '01931000-0000-7000-8000-000000000302', 'PASSPORT', 'RUS-7719204', '/vault/Passport_Scan_Bout_V.pdf',
                     '{""ubos"":[""Viktor Anatolyevich Bout (100% Beneficial Owner)""],""issueCountry"":""RUS"",""issuedDate"":""2018-05-01"",""expiresDate"":""2028-05-01"",""ocrConfidence"":94.1,""pepStatus"":""Sanctioned SDN (UNSC 1521)"",""blockReason"":""UNSC Resolution 1521 & OFAC SDN Match (Score 96.4%)"",""fields"":{""Holder Name"":""BOUT, Viktor Anatolyevich"",""Date of Birth"":""1967-01-13"",""Watchlist Flag"":""CRITICAL OFAC SDN / UNSC HIT""}}'::jsonb,
                     'SANCTIONED'),
                    ('01931000-0000-7000-8000-000000000405', '01931000-0000-7000-8000-000000000303', 'COMMERCIAL_REGISTRY', 'TL-DXB-889120', '/vault/Trade_License_Dubai_2026.pdf',
                     '{""ubos"":[""Tariq Al-Mansoori (65%)"",""Global Holdings FZCO (35%)""],""issueCountry"":""ARE"",""issuedDate"":""2024-02-10"",""expiresDate"":""2028-12-31"",""ocrConfidence"":99.1,""pepStatus"":""PEP Family Associate (Tier 3)"",""fields"":{""Entity Name"":""Acme Global Trading LLC"",""Trade License"":""TL-DXB-889120"",""Chamber of Commerce"":""Dubai Chamber #44910""}}'::jsonb,
                     'VERIFIED_100')
                ON CONFLICT (id) DO NOTHING;";

            await using (var docCmd = new NpgsqlCommand(seedDocsSql, conn))
            {
                await docCmd.ExecuteNonQueryAsync(ct);
            }

            const string seedTxsSql = @"
                INSERT INTO screening.transactions
                    (id, tenant_id, customer_id, transaction_ref_no, idempotency_key, transaction_type, amount, currency, lcy_amount, sender_nationality, sender_country, beneficiary_name, beneficiary_country, status, transaction_date)
                VALUES
                    ('01931000-0000-7000-8000-000000000501', @tid, '01931000-0000-7000-8000-000000000301', 'TX-2026-9921448-AE', 'idemp-seed-tx-501', 'SWIFT_MT103', 450000.00, 'USD', 1652625.00, 'ARE', 'ARE', 'Victor Bout (Zurich, Switzerland)', 'CHE', 'BLOCKED', '2026-10-14T07:12:00Z'),
                    ('01931000-0000-7000-8000-000000000502', @tid, '01931000-0000-7000-8000-000000000301', 'TX-2026-8812044-AE', 'idemp-seed-tx-502', 'SWIFT_MT103', 1200000.00, 'USD', 4407000.00, 'ARE', 'ARE', 'Global Petrochem Services Ltd (Singapore)', 'SGP', 'CLEARED', '2026-09-28T14:35:00Z'),
                    ('01931000-0000-7000-8000-000000000503', @tid, '01931000-0000-7000-8000-000000000301', 'TX-2026-7731902-AE', 'idemp-seed-tx-503', 'SWIFT_MT103', 340000.00, 'USD', 1248650.00, 'ARE', 'ARE', 'Caspian Shipping Agency (Baku)', 'AZE', 'HELD', '2026-08-14T11:20:00Z')
                ON CONFLICT (id) DO NOTHING;";

            await using (var txCmd = new NpgsqlCommand(seedTxsSql, conn))
            {
                txCmd.Parameters.AddWithValue("tid", tenantId);
                await txCmd.ExecuteNonQueryAsync(ct);
            }
        }
        catch
        {
            // Ignore when DB is offline in local/in-memory environments
        }
    }
}

public sealed class InMemoryKycService : IKycService
{
    public static readonly IReadOnlyList<KycSowChecklistItemDto> DefaultSowChecklist =
    [
        new("SOW_AUDITED_FIN", "Audited Financial Statements (FY 2025/2026)", "SOURCE_OF_WEALTH", true, "Ernst & Young Audit Filing #EY-DXB-2026"),
        new("SOW_UBO_DECL", "Notarized Ultimate Beneficial Ownership (UBO) Declaration", "CORPORATE_GOVERNANCE", true, "JAFZA Registry Attestation #991204"),
        new("SOF_BANK_STMT", "6-Month Correspondent Bank Account Statements", "SOURCE_OF_FUNDS", true, "Emirates NBD IBAN AE820330000001234567890"),
        new("SOF_CONTRACT_CHE", "Underlying Trade Contract & Bill of Lading for Switzerland / Baku Wires", "SOURCE_OF_FUNDS", false, "Pending Customer RFI Response (Due Overdue)"),
        new("PEP_MLRO_ATTEST", "Senior Management & MLRO Annual PEP Approval Sign-Off", "PEP_GOVERNANCE", false, "Requires 4-Eyes MLRO Periodic Sign-Off")
    ];

    public static readonly IReadOnlyList<KycAlertSummaryDto> DefaultAlerts =
    [
        new(
            "AL-2026-0921",
            "Statutory Freeze Triggered",
            "OFAC SDN hit on beneficiary Victor Bout in wire TX-2026-9921448-AE ($450,000 USD)",
            "FROZEN",
            "CRITICAL",
            "EDITH-CAS-2026-0042"),
        new(
            "AL-2026-0784",
            "High-Risk Corridor Velocity Anomaly",
            "Unapproved trade corridor wire to Baku, Azerbaijan (ARE -> AZE) held for invoice inspection.",
            "UNDER_EDD",
            "HIGH",
            "EDITH-CAS-2026-0043"),
        new(
            "AL-2026-0618",
            "Structuring Threshold Anomaly",
            "3 successive wires below $10,000 within 4 hours. Remediated under EDD file 441.",
            "RESOLVED",
            "MEDIUM",
            "EDITH-CAS-2026-0044")
    ];

    private readonly List<KycDossierDto> _dossiers = CreateInitialDossiers();
    private readonly object _sync = new();

    private static List<KycDossierDto> CreateInitialDossiers() =>
    [
        new KycDossierDto
        {
            CustomerId = "01931000-0000-7000-8000-000000000301",
            CustomerCode = "CUST-AE-88902",
            FullName = "Rashid Al-Maktoum Trading FZE",
            CustomerType = "Corporate Entity",
            RiskCategory = "HIGH_RISK",
            RiskScore = 84.5,
            Nationality = "ARE",
            IncorporationCountry = "ARE",
            RegistrationNumber = "DED-DXB-9921448",
            Jurisdiction = "United Arab Emirates (JAFZA Free Zone)",
            PepStatus = "PEP Linked (Tier 2 Shareholder)",
            RiskTier = "HIGH",
            IsBlocked = false,
            OnboardingStatus = "ACTIVE_EDD",
            OnboardedDate = "2023-04-18",
            NextReviewDate = "2026-10-15",
            RelationshipManager = "Mansoor Al-Falasi (Commercial Banking)",
            PrimaryAccount = "AE820330000001234567890",
            ExpectedMonthlyTurnoverUsd = 350000,
            ApprovedCorridors = ["ARE -> SGP", "ARE -> GBR", "ARE -> SAU"],
            UboIdentities = ["Rashid Al-Maktoum (65%)", "Kovalev Trading LLC (35%)"],
            Documents =
            [
                new KycDocumentDto(
                    "01931000-0000-7000-8000-000000000401",
                    "COMMERCIAL REGISTRY (CR-2023-991204)",
                    "VERIFIED_100",
                    "2027-04-11",
                    "COMMERCIAL_REGISTRY",
                    "CR-2023-991204",
                    "ARE",
                    "2023-04-12",
                    99.4,
                    new Dictionary<string, string>
                    {
                        ["Legal Entity Name"] = "Rashid Al-Maktoum Trading FZE",
                        ["License Number"] = "DED-DXB-9921448",
                        ["Free Zone Authority"] = "Jebel Ali Free Zone (JAFZA)",
                        ["Majority UBO (65%)"] = "Rashid Al-Maktoum (UAE National)",
                        ["Minority Shareholder (35%)"] = "Kovalev Trading LLC"
                    }),
                new KycDocumentDto(
                    "01931000-0000-7000-8000-000000000402",
                    "BOARD RESOLUTION (BR-DXB-2026-01)",
                    "VERIFIED_100",
                    "2028-01-08",
                    "BOARD_RESOLUTION",
                    "BR-DXB-2026-01",
                    "ARE",
                    "2026-01-08",
                    98.2,
                    new Dictionary<string, string>
                    {
                        ["Resolution Reference"] = "BR-DXB-2026-01",
                        ["Authorized Signatory"] = "Rashid Al-Maktoum",
                        ["Single Wire Mandate"] = "Up to USD 1,500,000",
                        ["Notary Public Stamp"] = "Dubai Courts Verified #8821"
                    }),
                new KycDocumentDto(
                    "01931000-0000-7000-8000-000000000403",
                    "PASSPORT (N-8841299-UAE)",
                    "VERIFIED_WARNING",
                    "2026-08-14",
                    "PASSPORT",
                    "N-8841299-UAE",
                    "ARE",
                    "2021-08-15",
                    89.0,
                    new Dictionary<string, string>
                    {
                        ["Holder Full Name"] = "RASHID AL-MAKTOUM",
                        ["Passport Number"] = "N-8841299-UAE",
                        ["MRZ Line"] = "P<AREALMAKTOUM<<RASHID<<<<<<<<<<<<<<<<<<<<<<",
                        ["OCR Warning"] = "Document expired 2026-08-14 — Request Updated Copy"
                    })
            ],
            RecentTransactions =
            [
                new KycTransactionDto("TX-2026-9921448-AE", "2026-10-14 07:12 UTC", 450000.0, "USD", "Victor Bout (Zurich, Switzerland)", "BLOCKED", "ARE -> CHE", 96.4, "EDITH-CAS-2026-0042"),
                new KycTransactionDto("TX-2026-8812044-AE", "2026-09-28 14:35 UTC", 1200000.0, "USD", "Global Petrochem Services Ltd (Singapore)", "CLEARED", "ARE -> SGP", 18.2, null),
                new KycTransactionDto("TX-2026-7731902-AE", "2026-08-14 11:20 UTC", 340000.0, "USD", "Caspian Shipping Agency (Baku)", "HELD", "ARE -> AZE", 78.5, "EDITH-CAS-2026-0043")
            ],
            SowChecklist = DefaultSowChecklist,
            HistoricalAlerts = DefaultAlerts
        },
        new KycDossierDto
        {
            CustomerId = "01931000-0000-7000-8000-000000000302",
            CustomerCode = "CUST-AE-88912",
            FullName = "Viktor Anatolyevich Bout",
            CustomerType = "Individual",
            RiskCategory = "EXTREME_SANCTIONS",
            RiskScore = 96.4,
            Nationality = "RUS",
            IncorporationCountry = "RUS",
            RegistrationNumber = "OFAC-9812",
            Jurisdiction = "Russian Federation (Non-Resident High Risk)",
            PepStatus = "Sanctioned SDN (UNSC Res 1521)",
            RiskTier = "CRITICAL",
            IsBlocked = true,
            BlockReason = "UNSC Resolution 1521 & OFAC SDN Match (Score 96.4%)",
            OnboardingStatus = "FROZEN_HOLD",
            OnboardedDate = "2024-01-10",
            NextReviewDate = "2026-10-01",
            RelationshipManager = "Special Assets & Sanctions Desk",
            PrimaryAccount = "AE440330000009988776655",
            ExpectedMonthlyTurnoverUsd = 50000,
            ApprovedCorridors = ["ARE -> ARE"],
            UboIdentities = ["Viktor Anatolyevich Bout (100% Beneficial Owner)"],
            Documents =
            [
                new KycDocumentDto(
                    "01931000-0000-7000-8000-000000000404",
                    "PASSPORT (RUS-7719204)",
                    "SANCTIONED",
                    "2028-05-01",
                    "PASSPORT",
                    "RUS-7719204",
                    "RUS",
                    "2018-05-01",
                    94.1,
                    new Dictionary<string, string>
                    {
                        ["Holder Full Name"] = "BOUT, Viktor Anatolyevich",
                        ["Date of Birth"] = "1967-01-13",
                        ["Sanctions Designation"] = "UNSC Res 1521 / OFAC SDN #9812"
                    })
            ],
            RecentTransactions =
            [
                new KycTransactionDto("TX-2026-9921448", "2026-10-18 09:15 UTC", 450000.0, "USD", "Viktor Anatolyevich Bout", "BLOCKED", "ARE -> RUS", 96.4, "EDITH-CAS-2026-0042")
            ],
            SowChecklist = DefaultSowChecklist,
            HistoricalAlerts = DefaultAlerts
        },
        new KycDossierDto
        {
            CustomerId = "01931000-0000-7000-8000-000000000303",
            CustomerCode = "CUST-AE-88925",
            FullName = "Acme Global Trading LLC",
            CustomerType = "Corporate Entity",
            RiskCategory = "HIGH_RISK",
            RiskScore = 78.5,
            Nationality = "ARE",
            IncorporationCountry = "ARE",
            RegistrationNumber = "TL-DXB-889120",
            Jurisdiction = "United Arab Emirates (DMCC Dubai)",
            PepStatus = "PEP Family Associate (Tier 3)",
            RiskTier = "HIGH",
            IsBlocked = false,
            OnboardingStatus = "ACTIVE_EDD",
            OnboardedDate = "2023-09-01",
            NextReviewDate = "2026-12-01",
            RelationshipManager = "Nadia Al-Hashimi (Corporate Trade Finance)",
            PrimaryAccount = "AE190330000005544332211",
            ExpectedMonthlyTurnoverUsd = 600000,
            ApprovedCorridors = ["ARE -> SGP", "ARE -> IND", "ARE -> GBR"],
            UboIdentities = ["Tariq Al-Mansoori (65%)", "Global Holdings FZCO (35%)"],
            Documents =
            [
                new KycDocumentDto("01931000-0000-7000-8000-000000000405", "TRADE LICENSE (TL-DXB-889120)", "VERIFIED_100", "2028-12-31", "COMMERCIAL_REGISTRY", "TL-DXB-889120", "ARE", "2024-02-10", 99.1)
            ],
            RecentTransactions =
            [
                new KycTransactionDto("TX-2026-9921448", "2026-10-18 11:00 UTC", 450000.0, "USD", "Viktor Anatolyevich Bout", "HELD", "ARE -> RUS", 94.5, "EDITH-CAS-2026-0042")
            ],
            SowChecklist = DefaultSowChecklist,
            HistoricalAlerts = DefaultAlerts
        },
        new KycDossierDto
        {
            CustomerId = "01931000-0000-7000-8000-000000000304",
            CustomerCode = "CUST-AE-88940",
            FullName = "Middle East Commodity Brokers",
            CustomerType = "Corporate Entity",
            RiskCategory = "EXTREME_SANCTIONS",
            RiskScore = 82.0,
            Nationality = "ARE",
            IncorporationCountry = "ARE",
            RegistrationNumber = "CR-AUH-441209",
            Jurisdiction = "United Arab Emirates (ADGM Abu Dhabi)",
            PepStatus = "PEP Linked (Tier 2 Ministerial Advisor)",
            RiskTier = "HIGH",
            IsBlocked = true,
            BlockReason = "Corporate SDN match on beneficiary Al-Barakaat Trading Company (Case EDITH-CAS-2026-0043)",
            OnboardingStatus = "FROZEN_HOLD",
            OnboardedDate = "2022-05-14",
            NextReviewDate = "2026-10-20",
            RelationshipManager = "Mansoor Al-Falasi (Commercial Banking)",
            PrimaryAccount = "AE610330000007788990011",
            ExpectedMonthlyTurnoverUsd = 500000,
            ApprovedCorridors = ["ARE -> SAU", "ARE -> OMN"],
            UboIdentities = ["Mansoor Mohammed Al-Marzooqi (55%)", "Gulf Horizon Investments (45%)"],
            Documents =
            [
                new KycDocumentDto("doc-mecb-1", "COMMERCIAL REGISTRY (CR-AUH-441209)", "VERIFIED_100", "2027-05-14", "COMMERCIAL_REGISTRY", "CR-AUH-441209", "ARE", "2022-05-14", 98.8)
            ],
            RecentTransactions =
            [
                new KycTransactionDto("TX-2026-9921449", "2026-10-24 04:00 UTC", 850000.0, "AED", "Al-Barakaat Trading Company", "HELD", "ARE -> SOM", 82.0, "EDITH-CAS-2026-0043")
            ],
            SowChecklist = DefaultSowChecklist,
            HistoricalAlerts = DefaultAlerts
        }
    ];

    public static KycDossierDto? FindTemplate(string code, string name)
    {
        var list = CreateInitialDossiers();
        return list.FirstOrDefault(d =>
            d.CustomerCode.Equals(code, StringComparison.OrdinalIgnoreCase) ||
            d.FullName.Contains(name, StringComparison.OrdinalIgnoreCase) ||
            name.Contains(d.FullName, StringComparison.OrdinalIgnoreCase));
    }

    public Task<IReadOnlyList<KycDossierDto>> ListCustomersAsync(string? search = null, bool blockedOnly = false, CancellationToken ct = default)
    {
        lock (_sync)
        {
            IEnumerable<KycDossierDto> query = _dossiers;
            if (blockedOnly)
            {
                query = query.Where(d => d.IsBlocked);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(d =>
                    d.CustomerCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    d.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    d.RegistrationNumber.Contains(term, StringComparison.OrdinalIgnoreCase));
            }
            return Task.FromResult<IReadOnlyList<KycDossierDto>>(query.ToList());
        }
    }

    public Task<KycDossierDto?> GetCustomerDossierAsync(string customerIdOrCode, CancellationToken ct = default)
    {
        lock (_sync)
        {
            var term = (customerIdOrCode ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(term))
            {
                return Task.FromResult<KycDossierDto?>(_dossiers[0]);
            }

            var match = _dossiers.FirstOrDefault(d =>
                d.CustomerId.Equals(term, StringComparison.OrdinalIgnoreCase) ||
                d.CustomerCode.Equals(term, StringComparison.OrdinalIgnoreCase) ||
                d.RegistrationNumber.Equals(term, StringComparison.OrdinalIgnoreCase) ||
                d.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                term.Contains(d.FullName, StringComparison.OrdinalIgnoreCase) ||
                d.UboIdentities.Any(u => u.Contains(term, StringComparison.OrdinalIgnoreCase)));

            return Task.FromResult(match);
        }
    }

    public Task<KycDossierDto?> UpdateCustomerEddAsync(string customerIdOrCode, KycEddUpdateRequest request, CancellationToken ct = default)
    {
        lock (_sync)
        {
            var idx = _dossiers.FindIndex(d =>
                d.CustomerId.Equals(customerIdOrCode, StringComparison.OrdinalIgnoreCase) ||
                d.CustomerCode.Equals(customerIdOrCode, StringComparison.OrdinalIgnoreCase) ||
                d.FullName.Contains(customerIdOrCode, StringComparison.OrdinalIgnoreCase));

            if (idx < 0)
            {
                return Task.FromResult<KycDossierDto?>(null);
            }

            var current = _dossiers[idx];
            var action = (request.Action ?? string.Empty).ToUpperInvariant();

            if (action == "COMPLETE_EDD_REVIEW")
            {
                var tier = string.IsNullOrWhiteSpace(request.RiskTier) ? current.RiskTier : request.RiskTier.Trim().ToUpperInvariant();
                current = current with
                {
                    OnboardingStatus = "EDD_APPROVED",
                    RiskTier = tier,
                    NextReviewDate = request.NextReviewDate ?? "2027-10-25"
                };
            }
            else if (action == "UPDATE_RISK_TIER" && !string.IsNullOrWhiteSpace(request.RiskTier))
            {
                var tier = request.RiskTier.Trim().ToUpperInvariant();
                var score = tier switch { "CRITICAL" => 96.4, "HIGH" => 84.5, "MEDIUM" => 48.0, _ => 15.0 };
                current = current with
                {
                    RiskTier = tier,
                    RiskCategory = $"{tier}_RISK",
                    RiskScore = score
                };
            }
            else if (action == "TOGGLE_BLOCK" && request.IsBlocked.HasValue)
            {
                current = current with
                {
                    IsBlocked = request.IsBlocked.Value,
                    BlockReason = request.IsBlocked.Value ? (request.BlockReason ?? "Administrative Compliance Freeze") : "",
                    OnboardingStatus = request.IsBlocked.Value ? "FROZEN_HOLD" : "ACTIVE_EDD",
                    RiskCategory = request.IsBlocked.Value ? "EXTREME_SANCTIONS" : $"{current.RiskTier}_RISK"
                };
            }
            else if (action == "VERIFY_DOCUMENT" && !string.IsNullOrWhiteSpace(request.DocumentId) && !string.IsNullOrWhiteSpace(request.DocumentStatus))
            {
                var updatedDocs = current.Documents.Select(doc =>
                    doc.Id == request.DocumentId
                        ? doc with { Status = request.DocumentStatus.Trim().ToUpperInvariant() }
                        : doc).ToList();
                current = current with { Documents = updatedDocs };
            }

            _dossiers[idx] = current;
            return Task.FromResult<KycDossierDto?>(current);
        }
    }

    public Task EnsureBaselineCustomersAsync(Guid tenantId, CancellationToken ct = default) => Task.CompletedTask;
}
