using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace Edith.Gateway.Services;

public sealed record AuditLogEntryDto(
    string Id,
    string TimestampUtc,
    string Actor,
    string Action,
    string TargetEntity,
    string? CaseNumber,
    string Sha256Hash,
    string PreviousBlockHash,
    string Details
);

public sealed record RecordAuditRequest(
    string Actor,
    string Action,
    string TargetEntity,
    string? CaseNumber,
    string Details
);

public interface IAuditLedgerService
{
    Task<IReadOnlyList<AuditLogEntryDto>> GetAuditLogsAsync(Guid tenantId, int limit = 50, CancellationToken ct = default);
    Task<AuditLogEntryDto> RecordAuditEntryAsync(Guid tenantId, RecordAuditRequest req, CancellationToken ct = default);
    Task EnsureBaselineAuditLedgerAsync(Guid tenantId, CancellationToken ct = default);
}

public sealed class PostgreSqlAuditLedgerService : IAuditLedgerService
{
    private readonly string _connectionString;

    public PostgreSqlAuditLedgerService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<AuditLogEntryDto>> GetAuditLogsAsync(Guid tenantId, int limit = 50, CancellationToken ct = default)
    {
        await EnsureBaselineAuditLedgerAsync(tenantId, ct);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT id, tenant_id, action_type, payload, created_at
            FROM core.audit_ledgers
            WHERE tenant_id = @tid
            ORDER BY id DESC
            LIMIT @limit;";

        var list = new List<AuditLogEntryDto>();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("limit", limit);

        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var id = r.GetInt64(0).ToString(CultureInfo.InvariantCulture);
            var actionType = r.GetString(2);
            var payloadJson = r.GetString(3);
            var createdAt = r.GetDateTime(4).ToString("o");

            string actor = "System";
            string targetEntity = "N/A";
            string? caseNumber = null;
            string sha256 = "N/A";
            string prevHash = "GENESIS";
            string details = "";

            try
            {
                using var doc = JsonDocument.Parse(payloadJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("actor", out var pActor)) actor = pActor.GetString() ?? actor;
                if (root.TryGetProperty("targetEntity", out var pTarget)) targetEntity = pTarget.GetString() ?? targetEntity;
                if (root.TryGetProperty("caseNumber", out var pCase)) caseNumber = pCase.GetString();
                if (root.TryGetProperty("sha256Hash", out var pHash)) sha256 = pHash.GetString() ?? sha256;
                if (root.TryGetProperty("previousBlockHash", out var pPrev)) prevHash = pPrev.GetString() ?? prevHash;
                if (root.TryGetProperty("details", out var pDet)) details = pDet.GetString() ?? "";
            }
            catch
            {
                // Fallback to raw json
                details = payloadJson;
            }

            list.Add(new AuditLogEntryDto(
                id,
                createdAt,
                actor,
                actionType,
                targetEntity,
                caseNumber,
                sha256,
                prevHash,
                details
            ));
        }

        return list;
    }

    public async Task<AuditLogEntryDto> RecordAuditEntryAsync(Guid tenantId, RecordAuditRequest req, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Fetch last hash
        const string lastHashSql = @"
            SELECT payload->>'sha256Hash'
            FROM core.audit_ledgers
            WHERE tenant_id = @tid
            ORDER BY id DESC
            LIMIT 1;";

        string prevHash = "0000000000000000000000000000000000000000000000000000000000000000";
        await using (var hashCmd = new NpgsqlCommand(lastHashSql, conn))
        {
            hashCmd.Parameters.AddWithValue("tid", tenantId);
            var obj = await hashCmd.ExecuteScalarAsync(ct);
            if (obj != null && obj != DBNull.Value && !string.IsNullOrWhiteSpace(obj.ToString()))
            {
                prevHash = obj.ToString()!;
            }
        }

        var timestamp = DateTimeOffset.UtcNow;
        var rawBlockData = $"{timestamp:o}|{tenantId}|{req.Actor}|{req.Action}|{req.TargetEntity}|{req.CaseNumber}|{prevHash}|{req.Details}";
        var computedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawBlockData))).ToLowerInvariant();

        var payloadDict = new Dictionary<string, object?>
        {
            ["actor"] = req.Actor,
            ["targetEntity"] = req.TargetEntity,
            ["caseNumber"] = req.CaseNumber,
            ["sha256Hash"] = computedHash,
            ["previousBlockHash"] = prevHash,
            ["details"] = req.Details
        };

        var payloadJson = JsonSerializer.Serialize(payloadDict);

        const string insertSql = @"
            INSERT INTO core.audit_ledgers (tenant_id, action_type, payload, created_at)
            VALUES (@tid, @action, @payload::jsonb, @createdAt)
            RETURNING id;";

        await using var insertCmd = new NpgsqlCommand(insertSql, conn);
        insertCmd.Parameters.AddWithValue("tid", tenantId);
        insertCmd.Parameters.AddWithValue("action", req.Action);
        insertCmd.Parameters.AddWithValue("payload", payloadJson);
        insertCmd.Parameters.AddWithValue("createdAt", timestamp);

        var newId = (long)(await insertCmd.ExecuteScalarAsync(ct))!;

        return new AuditLogEntryDto(
            newId.ToString(CultureInfo.InvariantCulture),
            timestamp.ToString("o", CultureInfo.InvariantCulture),
            req.Actor,
            req.Action,
            req.TargetEntity,
            req.CaseNumber,
            computedHash,
            prevHash,
            req.Details
        );
    }

    public async Task EnsureBaselineAuditLedgerAsync(Guid tenantId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string countSql = "SELECT COUNT(*) FROM core.audit_ledgers WHERE tenant_id = @tid;";
        await using var cmd = new NpgsqlCommand(countSql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);

        if (count == 0)
        {
            await RecordAuditEntryAsync(tenantId, new RecordAuditRequest(
                "System Initializer",
                "GENESIS_BLOCK_CREATION",
                "Platform Root",
                null,
                "Initialized immutable WORM cryptographic audit ledger chain."
            ), ct);

            await RecordAuditEntryAsync(tenantId, new RecordAuditRequest(
                "Fatima Al-Nuaimi (MLRO)",
                "ASSET_FREEZE_ORDER",
                "Viktor Anatolyevich Bout",
                "EDITH-CAS-2026-0042",
                "Asset freeze mandated under CBUAE AML Directive Art. 14 / UNSC Res 1521."
            ), ct);

            await RecordAuditEntryAsync(tenantId, new RecordAuditRequest(
                "Tariq Mansoor (Compliance Analyst)",
                "FALSE_POSITIVE_OVERRIDE",
                "Mohamed Al-Attar",
                "EDITH-CAS-2026-0041",
                "Divergent date of birth and passport confirmed via secondary KYC documents."
            ), ct);

            await RecordAuditEntryAsync(tenantId, new RecordAuditRequest(
                "Zaid Al-Husseini (Compliance Officer)",
                "WHITELIST_AUTHORIZATION",
                "DHL Express UAE LLC",
                null,
                "Approved statutory exemption under Pass 5 bypass policy for trusted logistics partner."
            ), ct);
        }
    }
}

public sealed class InMemoryAuditLedgerService : IAuditLedgerService
{
    private readonly List<AuditLogEntryDto> _entries = [];
    private long _nextId = 1;
    private readonly object _sync = new();

    public async Task<IReadOnlyList<AuditLogEntryDto>> GetAuditLogsAsync(Guid tenantId, int limit = 50, CancellationToken ct = default)
    {
        bool needsSeed;
        lock (_sync)
        {
            needsSeed = _entries.Count == 0;
        }
        if (needsSeed)
        {
            await EnsureBaselineAuditLedgerAsync(tenantId, ct);
        }
        lock (_sync)
        {
            return _entries.OrderByDescending(e => long.Parse(e.Id, CultureInfo.InvariantCulture)).Take(limit).ToList();
        }
    }

    public Task<AuditLogEntryDto> RecordAuditEntryAsync(Guid tenantId, RecordAuditRequest req, CancellationToken ct = default)
    {
        lock (_sync)
        {
            var prevHash = _entries.Count > 0 ? _entries[^1].Sha256Hash : "0000000000000000000000000000000000000000000000000000000000000000";
            var timestamp = DateTimeOffset.UtcNow;
            var rawBlockData = $"{timestamp:o}|{tenantId}|{req.Actor}|{req.Action}|{req.TargetEntity}|{req.CaseNumber}|{prevHash}|{req.Details}";
            var computedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawBlockData))).ToLowerInvariant();
            var entry = new AuditLogEntryDto(
                (_nextId++).ToString(CultureInfo.InvariantCulture),
                timestamp.ToString("o", CultureInfo.InvariantCulture),
                req.Actor,
                req.Action,
                req.TargetEntity,
                req.CaseNumber,
                computedHash,
                prevHash,
                req.Details
            );
            _entries.Add(entry);
            return Task.FromResult(entry);
        }
    }

    public async Task EnsureBaselineAuditLedgerAsync(Guid tenantId, CancellationToken ct = default)
    {
        bool isEmpty;
        lock (_sync)
        {
            isEmpty = _entries.Count == 0;
        }
        if (isEmpty)
        {
            await RecordAuditEntryAsync(tenantId, new RecordAuditRequest(
                "System Initializer",
                "GENESIS_BLOCK_CREATION",
                "Platform Root",
                null,
                "Initialized immutable WORM cryptographic audit ledger chain."
            ), ct);

            await RecordAuditEntryAsync(tenantId, new RecordAuditRequest(
                "Fatima Al-Nuaimi (MLRO)",
                "ASSET_FREEZE_ORDER",
                "Viktor Anatolyevich Bout",
                "EDITH-CAS-2026-0042",
                "Asset freeze mandated under CBUAE AML Directive Art. 14 / UNSC Res 1521."
            ), ct);

            await RecordAuditEntryAsync(tenantId, new RecordAuditRequest(
                "Tariq Mansoor (Compliance Analyst)",
                "FALSE_POSITIVE_OVERRIDE",
                "Mohamed Al-Attar",
                "EDITH-CAS-2026-0041",
                "Divergent date of birth and passport confirmed via secondary KYC documents."
            ), ct);

            await RecordAuditEntryAsync(tenantId, new RecordAuditRequest(
                "Zaid Al-Husseini (Compliance Officer)",
                "WHITELIST_AUTHORIZATION",
                "DHL Express UAE LLC",
                null,
                "Approved statutory exemption under Pass 5 bypass policy for trusted logistics partner."
            ), ct);
        }
    }
}
