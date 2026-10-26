using System.Data;
using Npgsql;

namespace Edith.Gateway.Services;

public sealed record ScoringWeightsDto(double NameWeightage, double NationalityWeightage, double DobWeightage, double IdWeightage, double DefaultThreshold);
public sealed record FatfTierDto(string Tier, IReadOnlyList<string> Countries, int RiskFactor);
public sealed record CurrencyThresholdDto(string Currency, decimal MandatoryReportingThreshold, decimal HighRiskApprovalThreshold);

public sealed record CountryMasterDto(string Code, string Name, bool IsHighRisk, decimal RiskScore, string FatfStatus);
public sealed record NationalityMasterDto(string Code, string Demonym, string Country, bool IsHighRisk, string RiskLevel);
public sealed record RiskRateSettingsDto(double CustHighRisk, double CustMedRisk, double TxHighRisk, double TxMedRisk);
public sealed record SetupControlsDto(bool CustomerScreening, bool BeneficiaryBankScreening, bool CorrespondentBankScreening, bool PartnerUboScreening, bool RealtimeHoldInterceptor);
public sealed record RiskAssessmentWeightsDto(double GeoWeight, double ClientTypeWeight, double ProductWeight);

public sealed record SystemMastersResponseDto(
    IReadOnlyList<FatfTierDto> FatfTiers,
    IReadOnlyList<CountryMasterDto> Countries,
    IReadOnlyList<NationalityMasterDto> Nationalities,
    IReadOnlyList<CurrencyThresholdDto> CurrencyThresholds,
    ScoringWeightsDto ScoringWeights,
    RiskRateSettingsDto RiskRates,
    SetupControlsDto SetupControls,
    RiskAssessmentWeightsDto RiskAssessment
);

public sealed record UpdateScoringWeightsRequest(double NameWeightage, double NationalityWeightage, double DobWeightage, double IdWeightage, double DefaultThreshold);
public sealed record UpdateCountryRiskRequest(string Code, string CountryName, bool IsHighRisk, decimal RiskScore, string? FatfStatus = null);
public sealed record UpdateNationalityRiskRequest(string Code, string Demonym, string Country, bool IsHighRisk, string RiskLevel);
public sealed record UpdateRiskRateSettingsRequest(double CustHighRisk, double CustMedRisk, double TxHighRisk, double TxMedRisk);
public sealed record UpdateSetupControlsRequest(bool CustomerScreening, bool BeneficiaryBankScreening, bool CorrespondentBankScreening, bool PartnerUboScreening, bool RealtimeHoldInterceptor);
public sealed record UpdateRiskAssessmentWeightsRequest(double GeoWeight, double ClientTypeWeight, double ProductWeight);
public sealed record UpdateCurrencyThresholdsRequest(IReadOnlyList<CurrencyThresholdDto> Thresholds);

public interface ISystemMastersService
{
    Task<SystemMastersResponseDto> GetMastersAsync(Guid tenantId, CancellationToken ct = default);
    Task<bool> UpdateScoringWeightsAsync(Guid tenantId, UpdateScoringWeightsRequest req, CancellationToken ct = default);
    Task<bool> UpsertCountryAsync(UpdateCountryRiskRequest req, CancellationToken ct = default);
    Task<bool> UpsertNationalityAsync(UpdateNationalityRiskRequest req, CancellationToken ct = default);
    Task<bool> UpdateRiskRatesAsync(Guid tenantId, UpdateRiskRateSettingsRequest req, CancellationToken ct = default);
    Task<bool> UpdateSetupControlsAsync(Guid tenantId, UpdateSetupControlsRequest req, CancellationToken ct = default);
    Task<bool> UpdateRiskAssessmentWeightsAsync(Guid tenantId, UpdateRiskAssessmentWeightsRequest req, CancellationToken ct = default);
    Task<bool> UpdateCurrencyThresholdsAsync(Guid tenantId, UpdateCurrencyThresholdsRequest req, CancellationToken ct = default);
}

public sealed class PostgreSqlSystemMastersService : ISystemMastersService
{
    private readonly string _connectionString;

    // In-memory cache for fallback and fast reads of tenant settings
    private static readonly Dictionary<Guid, RiskRateSettingsDto> _cachedRiskRates = new();
    private static readonly Dictionary<Guid, SetupControlsDto> _cachedSetupControls = new();
    private static readonly Dictionary<Guid, RiskAssessmentWeightsDto> _cachedRiskAssessment = new();
    private static readonly List<CurrencyThresholdDto> _cachedCurrencies = new()
    {
        new("AED", 55000m, 500000m),
        new("USD", 10000m, 100000m),
        new("EUR", 10000m, 100000m),
        new("GBP", 10000m, 100000m)
    };

    public PostgreSqlSystemMastersService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<SystemMastersResponseDto> GetMastersAsync(Guid tenantId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // 1. Fetch Country Tiers from rules.country_masters
        const string countrySql = "SELECT code, country_name, is_high_risk, risk_score FROM rules.country_masters ORDER BY risk_score DESC, country_name ASC;";
        var countries = new List<CountryMasterDto>();
        var blackList = new List<string>();
        var greyList = new List<string>();
        var monitored = new List<string>();

        await using (var cmd = new NpgsqlCommand(countrySql, conn))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct))
            {
                var code = r.GetString(0);
                var name = r.GetString(1);
                var isHigh = r.GetBoolean(2);
                var score = r.GetDecimal(3);

                string status;
                if (score >= 90m)
                {
                    status = "BLACK_LIST";
                    blackList.Add(code);
                }
                else if (score >= 60m || isHigh)
                {
                    status = "GREY_LIST";
                    greyList.Add(code);
                }
                else if (score >= 20m)
                {
                    status = "MONITORED";
                    monitored.Add(code);
                }
                else
                {
                    status = "COOPERATIVE";
                }

                countries.Add(new CountryMasterDto(code, name, isHigh, score, status));
            }
        }

        if (countries.Count == 0)
        {
            countries = new List<CountryMasterDto>
            {
                new("ARE", "United Arab Emirates", false, 10m, "COOPERATIVE"),
                new("SAU", "Saudi Arabia", false, 10m, "COOPERATIVE"),
                new("OMN", "Oman", false, 10m, "COOPERATIVE"),
                new("IND", "India", false, 20m, "MONITORED"),
                new("PAK", "Pakistan", false, 35m, "MONITORED"),
                new("GBR", "United Kingdom", false, 10m, "COOPERATIVE"),
                new("USA", "United States", false, 10m, "COOPERATIVE"),
                new("IRN", "Iran (Islamic Republic of)", true, 95m, "BLACK_LIST"),
                new("PRK", "North Korea (DPRK)", true, 99m, "BLACK_LIST"),
                new("SYR", "Syrian Arab Republic", true, 80m, "GREY_LIST"),
                new("RUS", "Russian Federation", true, 75m, "GREY_LIST"),
                new("MMR", "Myanmar", true, 92m, "BLACK_LIST")
            };
            blackList.AddRange(["IRN", "PRK", "MMR"]);
            greyList.AddRange(["SYR", "RUS"]);
            monitored.AddRange(["PAK", "IND"]);
        }

        var fatfTiers = new List<FatfTierDto>
        {
            new("BLACK_LIST", blackList, 100),
            new("GREY_LIST", greyList, 75),
            new("MONITORED", monitored, 60)
        };

        // 2. Fetch Nationality Masters from rules.nationality_masters
        const string nationalitySql = "SELECT code, nationality_name, is_high_risk, risk_score FROM rules.nationality_masters ORDER BY risk_score DESC, nationality_name ASC;";
        var nationalities = new List<NationalityMasterDto>();

        try
        {
            await using var cmdNat = new NpgsqlCommand(nationalitySql, conn);
            await using var rNat = await cmdNat.ExecuteReaderAsync(ct);
            while (await rNat.ReadAsync(ct))
            {
                var code = rNat.GetString(0);
                var demonym = rNat.GetString(1);
                var isHigh = rNat.GetBoolean(2);
                var score = rNat.GetDecimal(3);
                var level = score >= 75m || isHigh ? "HIGH" : (score >= 40m ? "MEDIUM" : "LOW");
                var country = demonym; // mapping demonym to country representation
                nationalities.Add(new NationalityMasterDto(code, demonym, country, isHigh, level));
            }
        }
        catch
        {
            // fallback handled below
        }

        if (nationalities.Count == 0)
        {
            nationalities = new List<NationalityMasterDto>
            {
                new("ARE", "Emirati", "United Arab Emirates", false, "LOW"),
                new("SAU", "Saudi", "Saudi Arabia", false, "LOW"),
                new("OMN", "Omani", "Oman", false, "LOW"),
                new("IND", "Indian", "India", false, "LOW"),
                new("PAK", "Pakistani", "Pakistan", false, "MEDIUM"),
                new("GBR", "British", "United Kingdom", false, "LOW"),
                new("USA", "American", "United States", false, "LOW"),
                new("IRN", "Iranian", "Iran", true, "HIGH"),
                new("PRK", "North Korean", "North Korea", true, "HIGH"),
                new("RUS", "Russian", "Russian Federation", true, "HIGH"),
                new("SYR", "Syrian", "Syria", true, "HIGH"),
                new("MMR", "Burmese", "Myanmar", true, "HIGH")
            };
        }

        // 3. Default or Cached Currency Thresholds
        var currencyThresholds = _cachedCurrencies.ToList();

        // 4. Fetch Scoring Weights from rules.screening_score_configs
        const string weightSql = @"
            SELECT name_weightage, nationality_weightage, dob_weightage, id_weightage, min_threshold_score
            FROM rules.screening_score_configs
            WHERE tenant_id = @tid
            LIMIT 1;";

        ScoringWeightsDto weights = new(60.0, 10.0, 15.0, 15.0, 80.0);

        try
        {
            await using var cmdW = new NpgsqlCommand(weightSql, conn);
            cmdW.Parameters.AddWithValue("tid", tenantId);
            await using var rW = await cmdW.ExecuteReaderAsync(ct);
            if (await rW.ReadAsync(ct))
            {
                weights = new ScoringWeightsDto(
                    (double)rW.GetDecimal(0),
                    (double)rW.GetDecimal(1),
                    (double)rW.GetDecimal(2),
                    (double)rW.GetDecimal(3),
                    (double)rW.GetDecimal(4)
                );
            }
        }
        catch
        {
            // use fallback
        }

        // 5. Fetch Risk Rate Settings
        var riskRates = _cachedRiskRates.TryGetValue(tenantId, out var rr) 
            ? rr 
            : new RiskRateSettingsDto(85, 60, 90, 70);

        // 6. Fetch Setup Controls
        var setupControls = _cachedSetupControls.TryGetValue(tenantId, out var sc)
            ? sc
            : new SetupControlsDto(true, true, true, true, true);

        // 7. Fetch Risk Assessment Weights
        var riskAssessment = _cachedRiskAssessment.TryGetValue(tenantId, out var ra)
            ? ra
            : new RiskAssessmentWeightsDto(30, 40, 30);

        return new SystemMastersResponseDto(
            fatfTiers,
            countries,
            nationalities,
            currencyThresholds,
            weights,
            riskRates,
            setupControls,
            riskAssessment
        );
    }

    public async Task<bool> UpdateScoringWeightsAsync(Guid tenantId, UpdateScoringWeightsRequest req, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string updateSql = @"
            UPDATE rules.screening_score_configs
            SET name_weightage = @nw,
                nationality_weightage = @natw,
                dob_weightage = @dobw,
                id_weightage = @idw,
                min_threshold_score = @min
            WHERE tenant_id = @tid;";

        await using var updateCmd = new NpgsqlCommand(updateSql, conn);
        updateCmd.Parameters.AddWithValue("tid", tenantId);
        updateCmd.Parameters.AddWithValue("nw", (decimal)req.NameWeightage);
        updateCmd.Parameters.AddWithValue("natw", (decimal)req.NationalityWeightage);
        updateCmd.Parameters.AddWithValue("dobw", (decimal)req.DobWeightage);
        updateCmd.Parameters.AddWithValue("idw", (decimal)req.IdWeightage);
        updateCmd.Parameters.AddWithValue("min", (decimal)req.DefaultThreshold);

        var rows = await updateCmd.ExecuteNonQueryAsync(ct);
        if (rows > 0) return true;

        await using var insertCmd = new NpgsqlCommand(@"
            INSERT INTO rules.screening_score_configs (id, tenant_id, name_weightage, nationality_weightage, dob_weightage, id_weightage, min_threshold_score)
            VALUES (gen_random_uuid(), @tid, @nw, @natw, @dobw, @idw, @min);", conn);
        insertCmd.Parameters.AddWithValue("tid", tenantId);
        insertCmd.Parameters.AddWithValue("nw", (decimal)req.NameWeightage);
        insertCmd.Parameters.AddWithValue("natw", (decimal)req.NationalityWeightage);
        insertCmd.Parameters.AddWithValue("dobw", (decimal)req.DobWeightage);
        insertCmd.Parameters.AddWithValue("idw", (decimal)req.IdWeightage);
        insertCmd.Parameters.AddWithValue("min", (decimal)req.DefaultThreshold);

        await insertCmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    public async Task<bool> UpsertCountryAsync(UpdateCountryRiskRequest req, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            INSERT INTO rules.country_masters (code, country_name, is_high_risk, risk_score)
            VALUES (@code, @name, @isHigh, @score)
            ON CONFLICT (code) DO UPDATE SET
                country_name = EXCLUDED.country_name,
                is_high_risk = EXCLUDED.is_high_risk,
                risk_score = EXCLUDED.risk_score;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("code", req.Code.Trim().ToUpperInvariant());
        cmd.Parameters.AddWithValue("name", req.CountryName.Trim());
        cmd.Parameters.AddWithValue("isHigh", req.IsHighRisk);
        cmd.Parameters.AddWithValue("score", req.RiskScore);

        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    public async Task<bool> UpsertNationalityAsync(UpdateNationalityRiskRequest req, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            INSERT INTO rules.nationality_masters (code, nationality_name, is_high_risk, risk_score)
            VALUES (@code, @name, @isHigh, @score)
            ON CONFLICT (code) DO UPDATE SET
                nationality_name = EXCLUDED.nationality_name,
                is_high_risk = EXCLUDED.is_high_risk,
                risk_score = EXCLUDED.risk_score;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("code", req.Code.Trim().ToUpperInvariant());
        cmd.Parameters.AddWithValue("name", req.Demonym.Trim());
        cmd.Parameters.AddWithValue("isHigh", req.IsHighRisk);
        cmd.Parameters.AddWithValue("score", req.IsHighRisk ? 80m : 15m);

        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    public Task<bool> UpdateRiskRatesAsync(Guid tenantId, UpdateRiskRateSettingsRequest req, CancellationToken ct = default)
    {
        _cachedRiskRates[tenantId] = new RiskRateSettingsDto(req.CustHighRisk, req.CustMedRisk, req.TxHighRisk, req.TxMedRisk);
        return Task.FromResult(true);
    }

    public Task<bool> UpdateSetupControlsAsync(Guid tenantId, UpdateSetupControlsRequest req, CancellationToken ct = default)
    {
        _cachedSetupControls[tenantId] = new SetupControlsDto(
            req.CustomerScreening,
            req.BeneficiaryBankScreening,
            req.CorrespondentBankScreening,
            req.PartnerUboScreening,
            req.RealtimeHoldInterceptor
        );
        return Task.FromResult(true);
    }

    public Task<bool> UpdateRiskAssessmentWeightsAsync(Guid tenantId, UpdateRiskAssessmentWeightsRequest req, CancellationToken ct = default)
    {
        _cachedRiskAssessment[tenantId] = new RiskAssessmentWeightsDto(req.GeoWeight, req.ClientTypeWeight, req.ProductWeight);
        return Task.FromResult(true);
    }

    public Task<bool> UpdateCurrencyThresholdsAsync(Guid tenantId, UpdateCurrencyThresholdsRequest req, CancellationToken ct = default)
    {
        _cachedCurrencies.Clear();
        _cachedCurrencies.AddRange(req.Thresholds);
        return Task.FromResult(true);
    }
}

public sealed class InMemorySystemMastersService : ISystemMastersService
{
    private ScoringWeightsDto _weights = new(60.0, 10.0, 15.0, 15.0, 80.0);
    private RiskRateSettingsDto _riskRates = new(85, 60, 90, 70);
    private SetupControlsDto _setupControls = new(true, true, true, true, true);
    private RiskAssessmentWeightsDto _riskAssessment = new(30, 40, 30);
    private readonly List<CurrencyThresholdDto> _currencyThresholds = new()
    {
        new("AED", 55000m, 500000m),
        new("USD", 10000m, 100000m),
        new("EUR", 10000m, 100000m),
        new("GBP", 10000m, 100000m)
    };
    private readonly List<CountryMasterDto> _countries = new()
    {
        new("ARE", "United Arab Emirates", false, 10m, "COOPERATIVE"),
        new("SAU", "Saudi Arabia", false, 10m, "COOPERATIVE"),
        new("OMN", "Oman", false, 10m, "COOPERATIVE"),
        new("IND", "India", false, 20m, "MONITORED"),
        new("PAK", "Pakistan", false, 35m, "MONITORED"),
        new("GBR", "United Kingdom", false, 10m, "COOPERATIVE"),
        new("USA", "United States", false, 10m, "COOPERATIVE"),
        new("IRN", "Iran (Islamic Republic of)", true, 95m, "BLACK_LIST"),
        new("PRK", "North Korea (DPRK)", true, 99m, "BLACK_LIST"),
        new("SYR", "Syrian Arab Republic", true, 80m, "GREY_LIST"),
        new("RUS", "Russian Federation", true, 75m, "GREY_LIST"),
        new("MMR", "Myanmar", true, 92m, "BLACK_LIST")
    };
    private readonly List<NationalityMasterDto> _nationalities = new()
    {
        new("ARE", "Emirati", "United Arab Emirates", false, "LOW"),
        new("SAU", "Saudi", "Saudi Arabia", false, "LOW"),
        new("OMN", "Omani", "Oman", false, "LOW"),
        new("IND", "Indian", "India", false, "LOW"),
        new("PAK", "Pakistani", "Pakistan", false, "MEDIUM"),
        new("GBR", "British", "United Kingdom", false, "LOW"),
        new("USA", "American", "United States", false, "LOW"),
        new("IRN", "Iranian", "Iran", true, "HIGH"),
        new("PRK", "North Korean", "North Korea", true, "HIGH"),
        new("RUS", "Russian", "Russian Federation", true, "HIGH"),
        new("SYR", "Syrian", "Syria", true, "HIGH"),
        new("MMR", "Burmese", "Myanmar", true, "HIGH")
    };

    public Task<SystemMastersResponseDto> GetMastersAsync(Guid tenantId, CancellationToken ct = default)
    {
        var fatfTiers = new List<FatfTierDto>
        {
            new("BLACK_LIST", _countries.Where(c => c.FatfStatus == "BLACK_LIST").Select(c => c.Code).ToList(), 100),
            new("GREY_LIST", _countries.Where(c => c.FatfStatus == "GREY_LIST").Select(c => c.Code).ToList(), 75),
            new("MONITORED", _countries.Where(c => c.FatfStatus == "MONITORED").Select(c => c.Code).ToList(), 60)
        };

        return Task.FromResult(new SystemMastersResponseDto(
            fatfTiers,
            _countries.ToList(),
            _nationalities.ToList(),
            _currencyThresholds.ToList(),
            _weights,
            _riskRates,
            _setupControls,
            _riskAssessment
        ));
    }

    public Task<bool> UpdateScoringWeightsAsync(Guid tenantId, UpdateScoringWeightsRequest req, CancellationToken ct = default)
    {
        _weights = new ScoringWeightsDto(req.NameWeightage, req.NationalityWeightage, req.DobWeightage, req.IdWeightage, req.DefaultThreshold);
        return Task.FromResult(true);
    }

    public Task<bool> UpsertCountryAsync(UpdateCountryRiskRequest req, CancellationToken ct = default)
    {
        var code = req.Code.Trim().ToUpperInvariant();
        _countries.RemoveAll(c => c.Code == code);
        var status = req.RiskScore >= 90m ? "BLACK_LIST" : (req.RiskScore >= 60m || req.IsHighRisk ? "GREY_LIST" : (req.RiskScore >= 20m ? "MONITORED" : "COOPERATIVE"));
        _countries.Add(new CountryMasterDto(code, req.CountryName, req.IsHighRisk, req.RiskScore, status));
        return Task.FromResult(true);
    }

    public Task<bool> UpsertNationalityAsync(UpdateNationalityRiskRequest req, CancellationToken ct = default)
    {
        var code = req.Code.Trim().ToUpperInvariant();
        _nationalities.RemoveAll(n => n.Code == code);
        _nationalities.Add(new NationalityMasterDto(code, req.Demonym, req.Country, req.IsHighRisk, req.RiskLevel));
        return Task.FromResult(true);
    }

    public Task<bool> UpdateRiskRatesAsync(Guid tenantId, UpdateRiskRateSettingsRequest req, CancellationToken ct = default)
    {
        _riskRates = new RiskRateSettingsDto(req.CustHighRisk, req.CustMedRisk, req.TxHighRisk, req.TxMedRisk);
        return Task.FromResult(true);
    }

    public Task<bool> UpdateSetupControlsAsync(Guid tenantId, UpdateSetupControlsRequest req, CancellationToken ct = default)
    {
        _setupControls = new SetupControlsDto(
            req.CustomerScreening,
            req.BeneficiaryBankScreening,
            req.CorrespondentBankScreening,
            req.PartnerUboScreening,
            req.RealtimeHoldInterceptor
        );
        return Task.FromResult(true);
    }

    public Task<bool> UpdateRiskAssessmentWeightsAsync(Guid tenantId, UpdateRiskAssessmentWeightsRequest req, CancellationToken ct = default)
    {
        _riskAssessment = new RiskAssessmentWeightsDto(req.GeoWeight, req.ClientTypeWeight, req.ProductWeight);
        return Task.FromResult(true);
    }

    public Task<bool> UpdateCurrencyThresholdsAsync(Guid tenantId, UpdateCurrencyThresholdsRequest req, CancellationToken ct = default)
    {
        _currencyThresholds.Clear();
        _currencyThresholds.AddRange(req.Thresholds);
        return Task.FromResult(true);
    }
}
