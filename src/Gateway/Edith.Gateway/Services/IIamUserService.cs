using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace Edith.Gateway.Services;

public sealed record IamUserDto(
    Guid Id,
    Guid TenantId,
    Guid? GroupId,
    string Username,
    string FullName,
    string Role,
    string Department,
    bool IsActive,
    bool MfaEnabled,
    string ShiftStatus,
    string LastLogin,
    string? PasswordHashScheme = "PBKDF2-SHA256"
);

public sealed record CreateIamUserRequest(
    string Username,
    string? Password,
    string? FullName,
    string Role,
    Guid? GroupId = null,
    string? Department = null,
    string? ShiftStatus = "ON_DUTY"
);

public sealed record UpdateIamUserRequest(
    string? Role = null,
    Guid? GroupId = null,
    bool? IsActive = null,
    string? ShiftStatus = null,
    string? Password = null
);

public sealed record IamGroupPermissionDto(
    string MenuId,
    bool CanView,
    bool CanEdit,
    bool CanAuthorize
);

public sealed record IamGroupDto(
    Guid Id,
    Guid TenantId,
    string GroupName,
    bool IsActive,
    IReadOnlyList<IamGroupPermissionDto> Permissions
);

public sealed record CreateIamGroupRequest(
    string GroupName,
    IReadOnlyList<IamGroupPermissionDto>? Permissions = null
);

public sealed record UpdateGroupPermissionsRequest(
    IReadOnlyList<IamGroupPermissionDto> Permissions
);

public static class IamPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);
        return $"pbkdf2_sha256${Iterations}${Convert.ToHexStringLower(salt)}${Convert.ToHexStringLower(hash)}";
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash)) return false;
        var parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2_sha256") return false;
        if (!int.TryParse(parts[1], CultureInfo.InvariantCulture, out var iterations)) return false;

        var salt = Convert.FromHexString(parts[2]);
        var expectedHash = Convert.FromHexString(parts[3]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }
}

public interface IIamUserService
{
    Task<IReadOnlyList<IamUserDto>> GetUsersAsync(Guid tenantId, CancellationToken ct = default);
    Task<IamUserDto> CreateUserAsync(Guid tenantId, CreateIamUserRequest req, CancellationToken ct = default);
    Task<IamUserDto?> UpdateUserAsync(Guid tenantId, Guid userId, UpdateIamUserRequest req, CancellationToken ct = default);
    Task<bool> DeleteUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<IamGroupDto>> GetGroupsAsync(Guid tenantId, CancellationToken ct = default);
    Task<IamGroupDto> CreateGroupAsync(Guid tenantId, CreateIamGroupRequest req, CancellationToken ct = default);
    Task<IamGroupDto?> UpdateGroupPermissionsAsync(Guid tenantId, Guid groupId, UpdateGroupPermissionsRequest req, CancellationToken ct = default);
}

public sealed class InMemoryIamUserService : IIamUserService
{
    private readonly ConcurrentDictionary<Guid, IamUserDto> _users = new();
    private readonly ConcurrentDictionary<Guid, string> _passwordHashes = new();
    private readonly ConcurrentDictionary<Guid, IamGroupDto> _groups = new();

    private static readonly Guid DefaultTenant = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public InMemoryIamUserService()
    {
        SeedDefaults();
    }

    private void SeedDefaults()
    {
        var mlroGroupId = Guid.Parse("01931000-0000-7000-8000-000000000501");
        var analystGroupId = Guid.Parse("01931000-0000-7000-8000-000000000502");
        var auditorGroupId = Guid.Parse("01931000-0000-7000-8000-000000000503");

        _groups[mlroGroupId] = new IamGroupDto(
            mlroGroupId,
            DefaultTenant,
            "MLRO_APPROVERS",
            true,
            [
                new("MENU_SCREENING", true, true, true),
                new("MENU_CASES", true, true, true),
                new("MENU_BULK", true, true, true),
                new("MENU_GOAML", true, true, true),
                new("MENU_RULES", true, true, true),
                new("MENU_AUDIT", true, false, false),
                new("MENU_MASTERS", true, true, true)
            ]);

        _groups[analystGroupId] = new IamGroupDto(
            analystGroupId,
            DefaultTenant,
            "L1_COMPLIANCE_ANALYSTS",
            true,
            [
                new("MENU_SCREENING", true, true, false),
                new("MENU_CASES", true, true, false),
                new("MENU_BULK", true, true, false),
                new("MENU_AUDIT", true, false, false)
            ]);

        _groups[auditorGroupId] = new IamGroupDto(
            auditorGroupId,
            DefaultTenant,
            "REGULATORY_AUDITORS",
            true,
            [
                new("MENU_SCREENING", true, false, false),
                new("MENU_CASES", true, false, false),
                new("MENU_GOAML", true, false, false),
                new("MENU_AUDIT", true, false, false)
            ]);

        var u1 = new IamUserDto(
            Guid.Parse("01931000-0000-7000-8000-000000000010"),
            DefaultTenant,
            mlroGroupId,
            "analyst.fatima@adcb.ae",
            "Fatima Al-Nuaimi",
            "MLRO",
            "Financial Crime Compliance (Unit 04)",
            true,
            true,
            "ON_DUTY",
            "2026-10-20 07:10 UTC");

        var u2 = new IamUserDto(
            Guid.Parse("01931000-0000-7000-8000-000000000020"),
            DefaultTenant,
            analystGroupId,
            "tariq.mansoor@adcb.ae",
            "Tariq Mansoor",
            "ANALYST",
            "Triage Desk",
            true,
            true,
            "ON_DUTY",
            "2026-10-20 06:45 UTC");

        var u3 = new IamUserDto(
            Guid.Parse("01931000-0000-7000-8000-000000000030"),
            DefaultTenant,
            auditorGroupId,
            "zayed.husseini@adcb.ae",
            "Zayed Al-Husseini",
            "AUDITOR",
            "Internal Audit & Inspection",
            true,
            true,
            "OFF_DUTY",
            "2026-10-19 14:15 UTC");

        var u4 = new IamUserDto(
            Guid.Parse("01931000-0000-7000-8000-000000000040"),
            DefaultTenant,
            mlroGroupId,
            "admin@edith.aml.cloud",
            "Infrastructure Admin",
            "SUPER_ADMIN",
            "DevOps & Architecture",
            true,
            true,
            "ON_DUTY",
            "2026-10-20 01:20 UTC");

        _users[u1.Id] = u1;
        _users[u2.Id] = u2;
        _users[u3.Id] = u3;
        _users[u4.Id] = u4;

        var defaultHash = IamPasswordHasher.HashPassword("EdithCompliance2026!");
        _passwordHashes[u1.Id] = defaultHash;
        _passwordHashes[u2.Id] = defaultHash;
        _passwordHashes[u3.Id] = defaultHash;
        _passwordHashes[u4.Id] = defaultHash;
    }

    public Task<IReadOnlyList<IamUserDto>> GetUsersAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = _users.Values
            .OrderByDescending(u => u.IsActive)
            .ThenBy(u => u.FullName)
            .ToList();
        return Task.FromResult<IReadOnlyList<IamUserDto>>(list);
    }

    public Task<IamUserDto> CreateUserAsync(Guid tenantId, CreateIamUserRequest req, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var fullName = !string.IsNullOrWhiteSpace(req.FullName)
            ? req.FullName.Trim()
            : req.Username.Split('@')[0].Replace('.', ' ');
        var role = string.IsNullOrWhiteSpace(req.Role) ? "ANALYST" : req.Role.Trim().ToUpperInvariant();
        var dept = !string.IsNullOrWhiteSpace(req.Department) ? req.Department.Trim() : "Financial Crime Compliance";
        var shift = !string.IsNullOrWhiteSpace(req.ShiftStatus) ? req.ShiftStatus.Trim().ToUpperInvariant() : "ON_DUTY";

        var user = new IamUserDto(
            id,
            tenantId,
            req.GroupId,
            req.Username.Trim(),
            fullName,
            role,
            dept,
            true,
            true,
            shift,
            DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture));

        _users[id] = user;
        _passwordHashes[id] = IamPasswordHasher.HashPassword(string.IsNullOrWhiteSpace(req.Password) ? "EdithDefault2026!" : req.Password);

        return Task.FromResult(user);
    }

    public Task<IamUserDto?> UpdateUserAsync(Guid tenantId, Guid userId, UpdateIamUserRequest req, CancellationToken ct = default)
    {
        if (!_users.TryGetValue(userId, out var existing))
        {
            return Task.FromResult<IamUserDto?>(null);
        }

        var updated = existing with
        {
            Role = !string.IsNullOrWhiteSpace(req.Role) ? req.Role.Trim().ToUpperInvariant() : existing.Role,
            GroupId = req.GroupId ?? existing.GroupId,
            IsActive = req.IsActive ?? existing.IsActive,
            ShiftStatus = !string.IsNullOrWhiteSpace(req.ShiftStatus) ? req.ShiftStatus.Trim().ToUpperInvariant() : existing.ShiftStatus
        };

        _users[userId] = updated;

        if (!string.IsNullOrWhiteSpace(req.Password))
        {
            _passwordHashes[userId] = IamPasswordHasher.HashPassword(req.Password);
        }

        return Task.FromResult<IamUserDto?>(updated);
    }

    public Task<bool> DeleteUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        _passwordHashes.TryRemove(userId, out _);
        return Task.FromResult(_users.TryRemove(userId, out _));
    }

    public Task<IReadOnlyList<IamGroupDto>> GetGroupsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = _groups.Values.OrderBy(g => g.GroupName).ToList();
        return Task.FromResult<IReadOnlyList<IamGroupDto>>(list);
    }

    public Task<IamGroupDto> CreateGroupAsync(Guid tenantId, CreateIamGroupRequest req, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var group = new IamGroupDto(
            id,
            tenantId,
            req.GroupName.Trim().ToUpperInvariant(),
            true,
            req.Permissions ?? []);
        _groups[id] = group;
        return Task.FromResult(group);
    }

    public Task<IamGroupDto?> UpdateGroupPermissionsAsync(Guid tenantId, Guid groupId, UpdateGroupPermissionsRequest req, CancellationToken ct = default)
    {
        if (!_groups.TryGetValue(groupId, out var existing))
        {
            return Task.FromResult<IamGroupDto?>(null);
        }

        var updated = existing with { Permissions = req.Permissions };
        _groups[groupId] = updated;
        return Task.FromResult<IamGroupDto?>(updated);
    }
}

public sealed class PostgreSqlIamUserService : IIamUserService
{
    private readonly string _connectionString;
    private readonly InMemoryIamUserService _fallback = new();

    public PostgreSqlIamUserService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<IamUserDto>> GetUsersAsync(Guid tenantId, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            const string sql = @"
                SELECT id, tenant_id, group_id, username, role, is_active, created_at
                FROM core.users
                WHERE tenant_id = @tid
                ORDER BY created_at DESC;";

            var list = new List<IamUserDto>();
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("tid", tenantId);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var uname = r.GetString(3);
                var role = r.GetString(4);
                var isActive = r.GetBoolean(5);
                var created = r.GetDateTime(6).ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

                list.Add(new IamUserDto(
                    r.GetGuid(0),
                    r.GetGuid(1),
                    r.IsDBNull(2) ? null : r.GetGuid(2),
                    uname,
                    uname.Split('@')[0].Replace('.', ' '),
                    role,
                    role == "MLRO" ? "Financial Crime Compliance (Unit 04)" : "Triage Desk",
                    isActive,
                    true,
                    isActive ? "ON_DUTY" : "OFF_DUTY",
                    created));
            }

            return list.Count > 0 ? list : await _fallback.GetUsersAsync(tenantId, ct);
        }
        catch (NpgsqlException)
        {
            return await _fallback.GetUsersAsync(tenantId, ct);
        }
    }

    public async Task<IamUserDto> CreateUserAsync(Guid tenantId, CreateIamUserRequest req, CancellationToken ct = default)
    {
        var created = await _fallback.CreateUserAsync(tenantId, req, ct);
        var passwordHash = IamPasswordHasher.HashPassword(string.IsNullOrWhiteSpace(req.Password) ? "EdithDefault2026!" : req.Password);

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using (var alterCmd = new NpgsqlCommand(
                "ALTER TABLE core.users ADD COLUMN IF NOT EXISTS password_hash VARCHAR(256);", conn))
            {
                await alterCmd.ExecuteNonQueryAsync(ct);
            }

            const string sql = @"
                INSERT INTO core.users (id, tenant_id, group_id, username, role, is_active, password_hash, created_at)
                VALUES (@id, @tid, @gid, @uname, @role, TRUE, @phash, NOW())
                ON CONFLICT (id) DO NOTHING;";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", created.Id);
            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("gid", (object?)req.GroupId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("uname", created.Username);
            cmd.Parameters.AddWithValue("role", created.Role);
            cmd.Parameters.AddWithValue("phash", passwordHash);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (NpgsqlException)
        {
            // Fallback already updated
        }

        return created;
    }

    public async Task<IamUserDto?> UpdateUserAsync(Guid tenantId, Guid userId, UpdateIamUserRequest req, CancellationToken ct = default)
    {
        var updated = await _fallback.UpdateUserAsync(tenantId, userId, req, ct);
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            const string sql = @"
                UPDATE core.users
                SET role = COALESCE(@role, role),
                    group_id = COALESCE(@gid, group_id),
                    is_active = COALESCE(@active, is_active)
                WHERE id = @id AND tenant_id = @tid;";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", userId);
            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("role", (object?)req.Role?.Trim().ToUpperInvariant() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("gid", (object?)req.GroupId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("active", (object?)req.IsActive ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (NpgsqlException)
        {
            // Fallback already updated
        }

        return updated;
    }

    public async Task<bool> DeleteUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var removed = await _fallback.DeleteUserAsync(tenantId, userId, ct);
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            const string sql = "DELETE FROM core.users WHERE id = @id AND tenant_id = @tid;";
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", userId);
            cmd.Parameters.AddWithValue("tid", tenantId);
            var rows = await cmd.ExecuteNonQueryAsync(ct);
            return rows > 0 || removed;
        }
        catch (NpgsqlException)
        {
            return removed;
        }
    }

    public async Task<IReadOnlyList<IamGroupDto>> GetGroupsAsync(Guid tenantId, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            const string groupSql = @"
                SELECT id, tenant_id, group_name, is_active
                FROM core.user_groups
                WHERE tenant_id = @tid
                ORDER BY group_name;";

            var groups = new List<(Guid Id, Guid TenantId, string Name, bool IsActive)>();
            await using (var cmd = new NpgsqlCommand(groupSql, conn))
            {
                cmd.Parameters.AddWithValue("tid", tenantId);
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    groups.Add((r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetBoolean(3)));
                }
            }

            if (groups.Count == 0)
            {
                return await _fallback.GetGroupsAsync(tenantId, ct);
            }

            var result = new List<IamGroupDto>();
            foreach (var g in groups)
            {
                const string permSql = @"
                    SELECT menu_id, can_view, can_edit, can_authorize
                    FROM core.group_menu_permissions
                    WHERE group_id = @gid;";

                var perms = new List<IamGroupPermissionDto>();
                await using var pCmd = new NpgsqlCommand(permSql, conn);
                pCmd.Parameters.AddWithValue("gid", g.Id);
                await using var pr = await pCmd.ExecuteReaderAsync(ct);
                while (await pr.ReadAsync(ct))
                {
                    perms.Add(new IamGroupPermissionDto(pr.GetString(0), pr.GetBoolean(1), pr.GetBoolean(2), pr.GetBoolean(3)));
                }

                result.Add(new IamGroupDto(g.Id, g.TenantId, g.Name, g.IsActive, perms));
            }

            return result;
        }
        catch (NpgsqlException)
        {
            return await _fallback.GetGroupsAsync(tenantId, ct);
        }
    }

    public async Task<IamGroupDto> CreateGroupAsync(Guid tenantId, CreateIamGroupRequest req, CancellationToken ct = default)
    {
        var group = await _fallback.CreateGroupAsync(tenantId, req, ct);
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            const string sql = @"
                INSERT INTO core.user_groups (id, tenant_id, group_name, is_active)
                VALUES (@id, @tid, @name, TRUE)
                ON CONFLICT (id) DO NOTHING;";

            await using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("id", group.Id);
                cmd.Parameters.AddWithValue("tid", tenantId);
                cmd.Parameters.AddWithValue("name", group.GroupName);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            if (req.Permissions != null)
            {
                await UpdateGroupPermissionsAsync(tenantId, group.Id, new UpdateGroupPermissionsRequest(req.Permissions), ct);
            }
        }
        catch (NpgsqlException)
        {
            // Stored in fallback
        }

        return group;
    }

    public async Task<IamGroupDto?> UpdateGroupPermissionsAsync(Guid tenantId, Guid groupId, UpdateGroupPermissionsRequest req, CancellationToken ct = default)
    {
        var updated = await _fallback.UpdateGroupPermissionsAsync(tenantId, groupId, req, ct);
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using (var delCmd = new NpgsqlCommand("DELETE FROM core.group_menu_permissions WHERE group_id = @gid;", conn))
            {
                delCmd.Parameters.AddWithValue("gid", groupId);
                await delCmd.ExecuteNonQueryAsync(ct);
            }

            foreach (var p in req.Permissions)
            {
                const string insSql = @"
                    INSERT INTO core.group_menu_permissions (id, group_id, menu_id, can_view, can_edit, can_authorize)
                    VALUES (@id, @gid, @menu, @view, @edit, @auth);";

                await using var insCmd = new NpgsqlCommand(insSql, conn);
                insCmd.Parameters.AddWithValue("id", Guid.NewGuid());
                insCmd.Parameters.AddWithValue("gid", groupId);
                insCmd.Parameters.AddWithValue("menu", p.MenuId);
                insCmd.Parameters.AddWithValue("view", p.CanView);
                insCmd.Parameters.AddWithValue("edit", p.CanEdit);
                insCmd.Parameters.AddWithValue("auth", p.CanAuthorize);
                await insCmd.ExecuteNonQueryAsync(ct);
            }
        }
        catch (NpgsqlException)
        {
            // Fallback already updated
        }

        return updated;
    }
}
