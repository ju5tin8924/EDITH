using System.Collections.Concurrent;
using Edith.Rules.Models;

namespace Edith.Rules.Repository;

public interface IRuleRepository
{
    Task<AmlRule> CreateRuleAsync(AmlRule rule, CancellationToken ct = default);
    Task<AmlRule?> GetRuleByCodeAsync(Guid tenantId, string ruleCode, CancellationToken ct = default);
    Task<IReadOnlyList<AmlRule>> ListRulesAsync(Guid tenantId, bool activeOnly = true, CancellationToken ct = default);
    Task<AmlRule> UpdateRuleAsync(AmlRule rule, CancellationToken ct = default);
    Task<bool> DeleteRuleAsync(Guid tenantId, Guid ruleId, CancellationToken ct = default);
}

public sealed class InMemoryRuleRepository : IRuleRepository
{
    private readonly ConcurrentDictionary<Guid, AmlRule> _rules = new();

    public Task<AmlRule> CreateRuleAsync(AmlRule rule, CancellationToken ct = default)
    {
        _rules[rule.Id] = rule;
        return Task.FromResult(rule);
    }

    public Task<AmlRule?> GetRuleByCodeAsync(Guid tenantId, string ruleCode, CancellationToken ct = default)
    {
        var found = _rules.Values.FirstOrDefault(r => r.TenantId == tenantId && string.Equals(r.RuleCode, ruleCode, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(found);
    }

    public Task<IReadOnlyList<AmlRule>> ListRulesAsync(Guid tenantId, bool activeOnly = true, CancellationToken ct = default)
    {
        var list = _rules.Values
            .Where(r => r.TenantId == tenantId && (!activeOnly || r.IsActive))
            .OrderBy(r => r.RuleCode)
            .ToList();

        return Task.FromResult<IReadOnlyList<AmlRule>>(list);
    }

    public Task<AmlRule> UpdateRuleAsync(AmlRule rule, CancellationToken ct = default)
    {
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        _rules[rule.Id] = rule;
        return Task.FromResult(rule);
    }

    public Task<bool> DeleteRuleAsync(Guid tenantId, Guid ruleId, CancellationToken ct = default)
    {
        if (_rules.TryGetValue(ruleId, out var rule) && rule.TenantId == tenantId)
        {
            return Task.FromResult(_rules.TryRemove(ruleId, out _));
        }

        return Task.FromResult(false);
    }
}

public sealed class PostgreSqlRuleRepository : IRuleRepository
{
    private readonly string _connectionString;

    public PostgreSqlRuleRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<AmlRule> CreateRuleAsync(AmlRule rule, CancellationToken ct = default)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            INSERT INTO rules.aml_rules (
                id, tenant_id, rule_code, rule_name, execution_mode, rule_type, risk_weightage, is_active,
                category, risk_level, threshold_amount, currency, high_risk_countries, window_minutes,
                min_transactions, structuring_min_amount, structuring_max_amount, created_at, updated_at
            )
            VALUES (
                @id, @tenant_id, @rule_code, @rule_name, 'REALTIME', @category, @weightage, @is_active,
                @category, @risk_level, @threshold_amount, @currency, @high_risk_countries, @window_minutes,
                @min_transactions, @structuring_min, @structuring_max, @created_at, @updated_at
            )
            ON CONFLICT (tenant_id, rule_code) DO UPDATE SET
                rule_name = EXCLUDED.rule_name,
                risk_weightage = EXCLUDED.risk_weightage,
                is_active = EXCLUDED.is_active,
                category = EXCLUDED.category,
                risk_level = EXCLUDED.risk_level,
                threshold_amount = EXCLUDED.threshold_amount,
                currency = EXCLUDED.currency,
                high_risk_countries = EXCLUDED.high_risk_countries,
                window_minutes = EXCLUDED.window_minutes,
                min_transactions = EXCLUDED.min_transactions,
                structuring_min_amount = EXCLUDED.structuring_min_amount,
                structuring_max_amount = EXCLUDED.structuring_max_amount,
                updated_at = EXCLUDED.updated_at;
            """;

        await BindRuleParameters(cmd: new Npgsql.NpgsqlCommand(sql, conn), rule).ExecuteNonQueryAsync(ct);
        return rule;
    }

    public async Task<AmlRule?> GetRuleByCodeAsync(Guid tenantId, string ruleCode, CancellationToken ct = default)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT id, tenant_id, rule_code, rule_name, category, risk_level, risk_weightage, is_active,
                   threshold_amount, currency, high_risk_countries, window_minutes, min_transactions,
                   structuring_min_amount, structuring_max_amount, created_at, updated_at
            FROM rules.aml_rules
            WHERE tenant_id = @tenant_id AND UPPER(rule_code) = UPPER(@rule_code);
            """;

        await using var cmd = new Npgsql.NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("rule_code", ruleCode);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return ReadRule(reader);
        }

        return null;
    }

    public async Task<IReadOnlyList<AmlRule>> ListRulesAsync(Guid tenantId, bool activeOnly = true, CancellationToken ct = default)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT id, tenant_id, rule_code, rule_name, category, risk_level, risk_weightage, is_active,
                   threshold_amount, currency, high_risk_countries, window_minutes, min_transactions,
                   structuring_min_amount, structuring_max_amount, created_at, updated_at
            FROM rules.aml_rules
            WHERE tenant_id = @tenant_id AND (@active_only = false OR is_active = true)
            ORDER BY rule_code;
            """;

        await using var cmd = new Npgsql.NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("active_only", activeOnly);

        var list = new List<AmlRule>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadRule(reader));
        }

        return list;
    }

    public async Task<AmlRule> UpdateRuleAsync(AmlRule rule, CancellationToken ct = default)
    {
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            UPDATE rules.aml_rules
            SET rule_code = @rule_code,
                rule_name = @rule_name,
                rule_type = @category,
                category = @category,
                risk_level = @risk_level,
                risk_weightage = @weightage,
                is_active = @is_active,
                threshold_amount = @threshold_amount,
                currency = @currency,
                high_risk_countries = @high_risk_countries,
                window_minutes = @window_minutes,
                min_transactions = @min_transactions,
                structuring_min_amount = @structuring_min,
                structuring_max_amount = @structuring_max,
                updated_at = @updated_at
            WHERE id = @id AND tenant_id = @tenant_id;
            """;

        await BindRuleParameters(new Npgsql.NpgsqlCommand(sql, conn), rule).ExecuteNonQueryAsync(ct);
        return rule;
    }

    public async Task<bool> DeleteRuleAsync(Guid tenantId, Guid ruleId, CancellationToken ct = default)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new Npgsql.NpgsqlCommand("DELETE FROM rules.aml_rules WHERE id = @id AND tenant_id = @tenant_id;", conn);
        cmd.Parameters.AddWithValue("id", ruleId);
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    private static Npgsql.NpgsqlCommand BindRuleParameters(Npgsql.NpgsqlCommand cmd, AmlRule rule)
    {
        cmd.Parameters.AddWithValue("id", rule.Id);
        cmd.Parameters.AddWithValue("tenant_id", rule.TenantId);
        cmd.Parameters.AddWithValue("rule_code", rule.RuleCode);
        cmd.Parameters.AddWithValue("rule_name", rule.RuleName);
        cmd.Parameters.AddWithValue("category", rule.Category.ToString());
        cmd.Parameters.AddWithValue("risk_level", rule.RiskLevel.ToString());
        cmd.Parameters.AddWithValue("weightage", rule.ScoreWeightage);
        cmd.Parameters.AddWithValue("is_active", rule.IsActive);
        cmd.Parameters.AddWithValue("threshold_amount", (object?)rule.ThresholdAmount ?? DBNull.Value);
        cmd.Parameters.AddWithValue("currency", (object?)rule.Currency ?? DBNull.Value);
        cmd.Parameters.AddWithValue("high_risk_countries", rule.HighRiskCountries != null ? (object)rule.HighRiskCountries.ToArray() : DBNull.Value);
        cmd.Parameters.AddWithValue("window_minutes", (object?)rule.WindowMinutes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("min_transactions", (object?)rule.MinTransactions ?? DBNull.Value);
        cmd.Parameters.AddWithValue("structuring_min", (object?)rule.StructuringMinAmount ?? DBNull.Value);
        cmd.Parameters.AddWithValue("structuring_max", (object?)rule.StructuringMaxAmount ?? DBNull.Value);
        cmd.Parameters.AddWithValue("created_at", rule.CreatedAt);
        cmd.Parameters.AddWithValue("updated_at", rule.UpdatedAt);
        return cmd;
    }

    private static AmlRule ReadRule(Npgsql.NpgsqlDataReader reader)
    {
        var catStr = reader.IsDBNull(4) ? "TransactionThreshold" : reader.GetString(4);
        var riskStr = reader.IsDBNull(5) ? "High" : reader.GetString(5);

        return new AmlRule
        {
            Id = reader.GetGuid(0),
            TenantId = reader.GetGuid(1),
            RuleCode = reader.GetString(2),
            RuleName = reader.GetString(3),
            Category = Enum.TryParse<RuleCategory>(catStr, true, out var c) ? c : RuleCategory.TransactionThreshold,
            RiskLevel = Enum.TryParse<RiskLevel>(riskStr, true, out var r) ? r : RiskLevel.High,
            ScoreWeightage = reader.GetDecimal(6),
            IsActive = reader.GetBoolean(7),
            ThresholdAmount = reader.IsDBNull(8) ? null : reader.GetDecimal(8),
            Currency = reader.IsDBNull(9) ? null : reader.GetString(9),
            HighRiskCountries = reader.IsDBNull(10) ? null : reader.GetFieldValue<string[]>(10).ToList(),
            WindowMinutes = reader.IsDBNull(11) ? null : reader.GetInt32(11),
            MinTransactions = reader.IsDBNull(12) ? null : reader.GetInt32(12),
            StructuringMinAmount = reader.IsDBNull(13) ? null : reader.GetDecimal(13),
            StructuringMaxAmount = reader.IsDBNull(14) ? null : reader.GetDecimal(14),
            CreatedAt = reader.IsDBNull(15) ? DateTimeOffset.UtcNow : reader.GetFieldValue<DateTimeOffset>(15),
            UpdatedAt = reader.IsDBNull(16) ? DateTimeOffset.UtcNow : reader.GetFieldValue<DateTimeOffset>(16)
        };
    }
}
