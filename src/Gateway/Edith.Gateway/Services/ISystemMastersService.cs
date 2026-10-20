using System.Data;
using Npgsql;

namespace Edith.Gateway.Services;

public sealed record ScoringWeightsDto(double NameWeightage, double NationalityWeightage, double DobWeightage, double IdWeightage, double DefaultThreshold);
public sealed record FatfTierDto(string Tier, IReadOnlyList<string> Countries, int RiskFactor);
public sealed record CurrencyThresholdDto(string Currency, decimal MandatoryReportingThreshold, decimal HighRiskApprovalThreshold);

public sealed record SystemMastersResponseDto(
    IReadOnlyList<FatfTierDto> FatfTiers,
    IReadOnlyList<CurrencyThresholdDto> CurrencyThresholds,
    ScoringWeightsDto ScoringWeights
);

public sealed record UpdateScoringWeightsRequest(double NameWeightage, double NationalityWeightage, double DobWeightage, double IdWeightage, double DefaultThreshold);
public sealed record UpdateCountryRiskRequest(string Code, string CountryName, bool IsHighRisk, decimal RiskScore);

public interface ISystemMastersService
{
    Task<SystemMastersResponseDto> GetMastersAsync(Guid tenantId, CancellationToken ct = default);
    Task<bool> UpdateScoringWeightsAsync(Guid tenantId, UpdateScoringWeightsRequest req, CancellationToken ct = default);
    Task<bool> UpsertCountryAsync(UpdateCountryRiskRequest req, CancellationToken ct = default);
}

public sealed class PostgreSqlSystemMastersService : ISystemMastersService
{
    private readonly string _connectionString;

    public PostgreSqlSystemMastersService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<SystemMastersResponseDto> GetMastersAsync(Guid tenantId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // 1. Fetch Country Tiers from rules.country_masters
        const string countrySql = "SELECT code, is_high_risk, risk_score FROM rules.country_masters ORDER BY risk_score DESC, code ASC;";
        var blackList = new List<string>();
        var greyList = new List<string>();
        var monitored = new List<string>();

        await using (var cmd = new NpgsqlCommand(countrySql, conn))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct))
            {
                var code = r.GetString(0);
                var isHigh = r.GetBoolean(1);
                var score = r.GetDecimal(2);

                if (score >= 90m)
                {
                    blackList.Add(code);
                }
                else if (score >= 60m || isHigh)
                {
                    greyList.Add(code);
                }
                else if (score >= 20m)
                {
                    monitored.Add(code);
                }
            }
        }

        if (blackList.Count == 0) blackList.AddRange(["IRN", "PRK", "MMR"]);
        if (greyList.Count == 0) greyList.AddRange(["SYR", "YEM", "RUS", "CYP"]);
        if (monitored.Count == 0) monitored.AddRange(["PAK", "CHN"]);

        var fatfTiers = new List<FatfTierDto>
        {
            new("BLACK_LIST", blackList, 100),
            new("GREY_LIST", greyList, 75),
            new("MONITORED", monitored, 60)
        };

        // 2. Default Currency Thresholds
        var currencyThresholds = new List<CurrencyThresholdDto>
        {
            new("AED", 55000m, 500000m),
            new("USD", 10000m, 100000m),
            new("EUR", 10000m, 100000m)
        };

        // 3. Fetch Scoring Weights from rules.screening_score_configs
        const string weightSql = @"
            SELECT name_weightage, nationality_weightage, dob_weightage, id_weightage, min_threshold_score
            FROM rules.screening_score_configs
            WHERE tenant_id = @tid
            LIMIT 1;";

        ScoringWeightsDto weights = new(60.0, 10.0, 15.0, 15.0, 80.0);

        await using (var cmd = new NpgsqlCommand(weightSql, conn))
        {
            cmd.Parameters.AddWithValue("tid", tenantId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                weights = new ScoringWeightsDto(
                    (double)r.GetDecimal(0),
                    (double)r.GetDecimal(1),
                    (double)r.GetDecimal(2),
                    (double)r.GetDecimal(3),
                    (double)r.GetDecimal(4)
                );
            }
        }

        return new SystemMastersResponseDto(fatfTiers, currencyThresholds, weights);
    }

    public async Task<bool> UpdateScoringWeightsAsync(Guid tenantId, UpdateScoringWeightsRequest req, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Check if existing record for tenant exists
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
}

public sealed class InMemorySystemMastersService : ISystemMastersService
{
    private ScoringWeightsDto _weights = new(60.0, 10.0, 15.0, 15.0, 80.0);
    private readonly List<string> _blackList = ["IRN", "PRK", "MMR"];
    private readonly List<string> _greyList = ["SYR", "YEM", "RUS", "CYP"];
    private readonly List<string> _monitored = ["PAK", "CHN"];

    public Task<SystemMastersResponseDto> GetMastersAsync(Guid tenantId, CancellationToken ct = default)
    {
        var fatfTiers = new List<FatfTierDto>
        {
            new("BLACK_LIST", _blackList.ToList(), 100),
            new("GREY_LIST", _greyList.ToList(), 75),
            new("MONITORED", _monitored.ToList(), 60)
        };
        var currencyThresholds = new List<CurrencyThresholdDto>
        {
            new("AED", 55000m, 500000m),
            new("USD", 10000m, 100000m),
            new("EUR", 10000m, 100000m)
        };
        return Task.FromResult(new SystemMastersResponseDto(fatfTiers, currencyThresholds, _weights));
    }

    public Task<bool> UpdateScoringWeightsAsync(Guid tenantId, UpdateScoringWeightsRequest req, CancellationToken ct = default)
    {
        _weights = new ScoringWeightsDto(req.NameWeightage, req.NationalityWeightage, req.DobWeightage, req.IdWeightage, req.DefaultThreshold);
        return Task.FromResult(true);
    }

    public Task<bool> UpsertCountryAsync(UpdateCountryRiskRequest req, CancellationToken ct = default)
    {
        var code = req.Code.Trim().ToUpperInvariant();
        _blackList.Remove(code);
        _greyList.Remove(code);
        _monitored.Remove(code);
        if (req.RiskScore >= 90m) _blackList.Add(code);
        else if (req.RiskScore >= 60m || req.IsHighRisk) _greyList.Add(code);
        else _monitored.Add(code);
        return Task.FromResult(true);
    }
}
