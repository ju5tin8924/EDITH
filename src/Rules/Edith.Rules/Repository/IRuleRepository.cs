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
