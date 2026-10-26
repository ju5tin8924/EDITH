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
    string? PasswordHashScheme = "PBKDF2-SHA256",
    bool MustChangePassword = false,
    string? InvitationToken = null
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
    string? FullName = null,
    string? Department = null,
    string? Role = null,
    Guid? GroupId = null,
    bool? IsActive = null,
    string? ShiftStatus = null,
    string? Password = null,
    bool? MfaEnabled = null,
    bool? MustChangePassword = null
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

public sealed record MenuMasterDto(
    Guid Id,
    Guid? ParentMenuId,
    string MenuName,
    string MenuCode,
    string Path,
    int Position,
    string Icon,
    bool IsActive
);

public sealed record GroupMenuMappingDto(
    Guid GroupId,
    Guid MenuId,
    string MenuName,
    string MenuCode,
    string Path,
    bool CanView,
    bool CanEdit,
    bool CanAuthorize
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
    Task<IamUserDto?> GetUserByIdAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<IamUserDto?> GetUserByUsernameAsync(Guid tenantId, string username, CancellationToken ct = default);
    Task<IamUserDto?> GetUserByInvitationTokenAsync(string token, CancellationToken ct = default);
    Task<IamUserDto> CreateUserAsync(Guid tenantId, CreateIamUserRequest req, CancellationToken ct = default);
    Task<(IamUserDto User, string RawPassword, string InvitationToken)> CreateUserWithTokenAsync(Guid tenantId, CreateIamUserRequest req, CancellationToken ct = default);
    Task<IamUserDto?> UpdateUserAsync(Guid tenantId, Guid userId, UpdateIamUserRequest req, CancellationToken ct = default);
    Task<bool> DeleteUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<bool> VerifyPasswordAsync(Guid tenantId, string username, string password, CancellationToken ct = default);
    Task<string?> GetMfaSecretAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<bool> CompleteOnboardingAsync(string token, string newPassword, string mfaSecret, IReadOnlyList<string> recoveryCodes, CancellationToken ct = default);
    Task<bool> VerifyAndConsumeRecoveryCodeAsync(Guid tenantId, Guid userId, string code, CancellationToken ct = default);
    Task<string> RegenerateInvitationTokenAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<Guid?> FindTenantIdByUsernameAsync(string username, CancellationToken ct = default);

    Task<IReadOnlyList<IamGroupDto>> GetGroupsAsync(Guid tenantId, CancellationToken ct = default);
    Task<IamGroupDto> CreateGroupAsync(Guid tenantId, CreateIamGroupRequest req, CancellationToken ct = default);
    Task<IamGroupDto?> UpdateGroupPermissionsAsync(Guid tenantId, Guid groupId, UpdateGroupPermissionsRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<MenuMasterDto>> GetMenusAsync(CancellationToken ct = default);
    Task<IReadOnlyList<GroupMenuMappingDto>> GetMenuMappingsAsync(Guid tenantId, Guid groupId, CancellationToken ct = default);
}

public sealed class InMemoryIamUserService : IIamUserService
{
    private readonly ConcurrentDictionary<Guid, IamUserDto> _users = new();
    private readonly ConcurrentDictionary<Guid, string> _passwordHashes = new();
    private readonly ConcurrentDictionary<Guid, string> _mfaSecrets = new();
    private readonly ConcurrentDictionary<Guid, List<string>> _recoveryCodeHashes = new();
    private readonly ConcurrentDictionary<Guid, IamGroupDto> _groups = new();
    private readonly List<MenuMasterDto> _menus = [];

    private static readonly Guid DefaultTenant = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public InMemoryIamUserService()
    {
        SeedDefaults();
    }

    private void SeedDefaults()
    {
        // 1. Seed Groups
        var adminGroupId = Guid.Parse("01932000-0000-7000-8000-000000000A01");
        var amlOfficerGroupId = Guid.Parse("01932000-0000-7000-8000-000000000A02");
        var eddAnalystGroupId = Guid.Parse("01932000-0000-7000-8000-000000000A03");
        var istrMlroGroupId = Guid.Parse("01932000-0000-7000-8000-000000000A04");

        _groups[adminGroupId] = new IamGroupDto(
            adminGroupId, DefaultTenant, "ADMINISTRATOR_GROUP", true,
            [
                new("MENU_DASHBOARD", true, true, true),
                new("MENU_BLACKLIST", true, true, true),
                new("MENU_WHITELIST", true, true, true),
                new("MENU_AML", true, true, true),
                new("MENU_STR_PROCESS", true, true, true),
                new("MENU_REPORTS", true, true, true),
                new("MENU_LOG_REPORT", true, true, true),
                new("MENU_SETTINGS", true, true, true),
                new("MENU_MASTER", true, true, true),
                new("MENU_AUTHORIZATION", true, true, true),
                new("MENU_CUSTOMER", true, true, true),
                new("MENU_DELTA", true, true, true)
            ]);

        _groups[amlOfficerGroupId] = new IamGroupDto(
            amlOfficerGroupId, DefaultTenant, "AML_AUTHORIZED_OFFICERS", true,
            [
                new("MENU_DASHBOARD", true, true, false),
                new("MENU_AUTHORIZATION", true, true, true),
                new("MENU_STR_PROCESS", true, true, false),
                new("MENU_AML", true, true, true),
                new("MENU_BLACKLIST", true, true, true),
                new("MENU_WHITELIST", true, true, true),
                new("MENU_DELTA", true, true, true)
            ]);

        _groups[eddAnalystGroupId] = new IamGroupDto(
            eddAnalystGroupId, DefaultTenant, "EDD_INVESTIGATION_DESK", true,
            [
                new("MENU_DASHBOARD", true, false, false),
                new("MENU_STR_PROCESS", true, true, false),
                new("MENU_CUSTOMER", true, true, false),
                new("MENU_WHITELIST", true, true, false)
            ]);

        _groups[istrMlroGroupId] = new IamGroupDto(
            istrMlroGroupId, DefaultTenant, "MLRO_APPROVAL_DEPT", true,
            [
                new("MENU_DASHBOARD", true, true, true),
                new("MENU_STR_PROCESS", true, true, true),
                new("MENU_AML", true, true, true),
                new("MENU_REPORTS", true, true, true),
                new("MENU_LOG_REPORT", true, false, false),
                new("MENU_CUSTOMER", true, true, true)
            ]);

        // 2. Pre-loaded Users
        var defaultHash = IamPasswordHasher.HashPassword("EdithCompliance2026!");
        var defaultMfaSecret = "JBSWY3DPEHPK3PXP"; // Test Base32 secret

        // 1. Super Admin
        var uSuper = new IamUserDto(
            Guid.Parse("01932000-0000-7000-8000-000000000001"),
            DefaultTenant,
            adminGroupId,
            "superadmin@edith.aml.cloud",
            "Platform Super Administrator",
            "SUPER_ADMIN",
            "Global Security & Architecture",
            true,
            true,
            "ON_DUTY",
            "2026-10-21 08:30 UTC",
            MustChangePassword: false);

        // 2. Administrator
        var uAdmin = new IamUserDto(
            Guid.Parse("01932000-0000-7000-8000-000000000002"),
            DefaultTenant,
            adminGroupId,
            "admin@edith.aml.cloud",
            "Tenant Compliance Administrator",
            "ADMIN",
            "FCC Administration Desk",
            true,
            true,
            "ON_DUTY",
            "2026-10-21 08:15 UTC",
            MustChangePassword: false);

        // 3. AML Officer
        var uOfficer = new IamUserDto(
            Guid.Parse("01932000-0000-7000-8000-000000000003"),
            DefaultTenant,
            amlOfficerGroupId,
            "aml.officer@adcb.ae",
            "Tariq Mansoor",
            "AML_OFFICER",
            "Financial Crime Compliance - L1 Desk",
            true,
            true,
            "ON_DUTY",
            "2026-10-21 07:45 UTC",
            MustChangePassword: false);

        // 4. EDD Analyst
        var uEdd = new IamUserDto(
            Guid.Parse("01932000-0000-7000-8000-000000000004"),
            DefaultTenant,
            eddAnalystGroupId,
            "edd.analyst@adcb.ae",
            "Sara Ahmed",
            "EDD_ANALYST",
            "Enhanced Due Diligence & Investigations",
            true,
            true,
            "ON_DUTY",
            "2026-10-21 07:20 UTC",
            MustChangePassword: false);

        // 5. ISTR MLRO
        var uMlro = new IamUserDto(
            Guid.Parse("01932000-0000-7000-8000-000000000005"),
            DefaultTenant,
            istrMlroGroupId,
            "istr.mlro@adcb.ae",
            "Fatima Al-Nuaimi",
            "ISTR_MLRO",
            "Financial Crime Compliance (Unit 04 MLRO)",
            true,
            true,
            "ON_DUTY",
            "2026-10-21 08:00 UTC",
            MustChangePassword: false);

        _users[uSuper.Id] = uSuper;
        _users[uAdmin.Id] = uAdmin;
        _users[uOfficer.Id] = uOfficer;
        _users[uEdd.Id] = uEdd;
        _users[uMlro.Id] = uMlro;

        _passwordHashes[uSuper.Id] = defaultHash;
        _passwordHashes[uAdmin.Id] = defaultHash;
        _passwordHashes[uOfficer.Id] = defaultHash;
        _passwordHashes[uEdd.Id] = defaultHash;
        _passwordHashes[uMlro.Id] = defaultHash;

        _mfaSecrets[uSuper.Id] = defaultMfaSecret;
        _mfaSecrets[uAdmin.Id] = defaultMfaSecret;
        _mfaSecrets[uOfficer.Id] = defaultMfaSecret;
        _mfaSecrets[uEdd.Id] = defaultMfaSecret;
        _mfaSecrets[uMlro.Id] = defaultMfaSecret;

        // Populate initial recovery codes
        var totp = new TotpService();
        foreach (var u in new[] { uSuper.Id, uAdmin.Id, uOfficer.Id, uEdd.Id, uMlro.Id })
        {
            var codes = totp.GenerateRecoveryCodes(8);
            _recoveryCodeHashes[u] = codes.Select(c => totp.HashRecoveryCode(c)).ToList();
        }

        // 3. Seed Menus Master
        SeedMenus();
    }

    private void SeedMenus()
    {
        var mainMenus = new[]
        {
            ("MENU_DASHBOARD", "Home / Dashboard", "/dashboard", 1, "BarChart3"),
            ("MENU_BLACKLIST", "Blacklist", "/blacklist/screening", 2, "ShieldAlert"),
            ("MENU_WHITELIST", "White List", "/whitelist", 3, "ShieldCheck"),
            ("MENU_AML", "AML", "/aml/rules", 4, "Sliders"),
            ("MENU_STR_PROCESS", "STR Process", "/str-process/alerts", 5, "Briefcase"),
            ("MENU_REPORTS", "Reports", "/reports/alerts", 6, "FileSpreadsheet"),
            ("MENU_LOG_REPORT", "Log Report", "/log-report/process", 7, "FileText"),
            ("MENU_SETTINGS", "Settings", "/settings/users", 8, "Settings"),
            ("MENU_MASTER", "Master", "/master/country", 9, "Database"),
            ("MENU_AUTHORIZATION", "Authorization", "/authorization/sanction-alerts", 10, "BadgeCheck"),
            ("MENU_CUSTOMER", "Customer", "/customer/kyc", 11, "UserCheck"),
            ("MENU_DELTA", "Delta", "/delta/authorization", 12, "RefreshCw")
        };

        foreach (var m in mainMenus)
        {
            _menus.Add(new MenuMasterDto(Guid.NewGuid(), null, m.Item2, m.Item1, m.Item3, m.Item4, m.Item5, true));
        }
    }

    public Task<IReadOnlyList<IamUserDto>> GetUsersAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = _users.Values
            .OrderByDescending(u => u.IsActive)
            .ThenBy(u => u.FullName)
            .ToList();
        return Task.FromResult<IReadOnlyList<IamUserDto>>(list);
    }

    public Task<IamUserDto?> GetUserByIdAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        _users.TryGetValue(userId, out var user);
        return Task.FromResult(user);
    }

    public Task<IamUserDto?> GetUserByUsernameAsync(Guid tenantId, string username, CancellationToken ct = default)
    {
        var user = _users.Values.FirstOrDefault(u => 
            string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(user);
    }

    public Task<IamUserDto?> GetUserByInvitationTokenAsync(string token, CancellationToken ct = default)
    {
        var user = _users.Values.FirstOrDefault(u => 
            string.Equals(u.InvitationToken, token, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(user);
    }

    public async Task<IamUserDto> CreateUserAsync(Guid tenantId, CreateIamUserRequest req, CancellationToken ct = default)
    {
        var (user, _, _) = await CreateUserWithTokenAsync(tenantId, req, ct);
        return user;
    }

    public Task<(IamUserDto User, string RawPassword, string InvitationToken)> CreateUserWithTokenAsync(
        Guid tenantId, CreateIamUserRequest req, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var fullName = !string.IsNullOrWhiteSpace(req.FullName)
            ? req.FullName.Trim()
            : req.Username.Split('@')[0].Replace('.', ' ');

        var role = string.IsNullOrWhiteSpace(req.Role) ? "AML_OFFICER" : req.Role.Trim().ToUpperInvariant();
        var dept = !string.IsNullOrWhiteSpace(req.Department) ? req.Department.Trim() : "Financial Crime Compliance";
        var shift = !string.IsNullOrWhiteSpace(req.ShiftStatus) ? req.ShiftStatus.Trim().ToUpperInvariant() : "ON_DUTY";

        var rawPassword = !string.IsNullOrWhiteSpace(req.Password)
            ? req.Password.Trim()
            : CryptoPasswordGenerator.GenerateSecurePassword(16);

        var invitationToken = CryptoPasswordGenerator.GenerateInvitationToken();

        var user = new IamUserDto(
            id,
            tenantId,
            req.GroupId,
            req.Username.Trim(),
            fullName,
            role,
            dept,
            true,
            false, // 2FA is pending until user completes onboarding
            shift,
            "Never (Pending Activation)",
            PasswordHashScheme: "PBKDF2-SHA256",
            MustChangePassword: true,
            InvitationToken: invitationToken);

        _users[id] = user;
        _passwordHashes[id] = IamPasswordHasher.HashPassword(rawPassword);

        return Task.FromResult((user, rawPassword, invitationToken));
    }

    public Task<IamUserDto?> UpdateUserAsync(Guid tenantId, Guid userId, UpdateIamUserRequest req, CancellationToken ct = default)
    {
        if (!_users.TryGetValue(userId, out var existing))
        {
            return Task.FromResult<IamUserDto?>(null);
        }

        var updated = existing with
        {
            FullName = !string.IsNullOrWhiteSpace(req.FullName) ? req.FullName.Trim() : existing.FullName,
            Department = !string.IsNullOrWhiteSpace(req.Department) ? req.Department.Trim() : existing.Department,
            Role = !string.IsNullOrWhiteSpace(req.Role) ? req.Role.Trim().ToUpperInvariant() : existing.Role,
            GroupId = req.GroupId ?? existing.GroupId,
            IsActive = req.IsActive ?? existing.IsActive,
            ShiftStatus = !string.IsNullOrWhiteSpace(req.ShiftStatus) ? req.ShiftStatus.Trim().ToUpperInvariant() : existing.ShiftStatus,
            MfaEnabled = req.MfaEnabled ?? existing.MfaEnabled,
            MustChangePassword = req.MustChangePassword ?? existing.MustChangePassword
        };

        if (req.MfaEnabled == false)
        {
            _mfaSecrets.TryRemove(userId, out _);
            _recoveryCodeHashes.TryRemove(userId, out _);
        }

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
        _mfaSecrets.TryRemove(userId, out _);
        _recoveryCodeHashes.TryRemove(userId, out _);
        return Task.FromResult(_users.TryRemove(userId, out _));
    }

    public Task<bool> VerifyPasswordAsync(Guid tenantId, string username, string password, CancellationToken ct = default)
    {
        var user = _users.Values.FirstOrDefault(u => 
            string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
        if (user == null) return Task.FromResult(false);

        if (_passwordHashes.TryGetValue(user.Id, out var hash))
        {
            return Task.FromResult(IamPasswordHasher.VerifyPassword(password, hash));
        }

        return Task.FromResult(false);
    }

    public Task<string?> GetMfaSecretAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        _mfaSecrets.TryGetValue(userId, out var secret);
        return Task.FromResult(secret);
    }

    public Task<Guid?> FindTenantIdByUsernameAsync(string username, CancellationToken ct = default)
    {
        var user = _users.Values.FirstOrDefault(u => 
            string.Equals(u.Username, username.Trim(), StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(user?.TenantId);
    }

    public Task<bool> CompleteOnboardingAsync(
        string token, string newPassword, string mfaSecret, IReadOnlyList<string> recoveryCodes, CancellationToken ct = default)
    {
        var user = _users.Values.FirstOrDefault(u => 
            string.Equals(u.InvitationToken, token, StringComparison.OrdinalIgnoreCase));
        if (user == null) return Task.FromResult(false);

        var updated = user with
        {
            MustChangePassword = false,
            MfaEnabled = true,
            InvitationToken = null,
            LastLogin = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)
        };

        _users[user.Id] = updated;
        _passwordHashes[user.Id] = IamPasswordHasher.HashPassword(newPassword);
        _mfaSecrets[user.Id] = mfaSecret;

        var totp = new TotpService();
        _recoveryCodeHashes[user.Id] = recoveryCodes.Select(c => totp.HashRecoveryCode(c)).ToList();

        return Task.FromResult(true);
    }

    public Task<bool> VerifyAndConsumeRecoveryCodeAsync(Guid tenantId, Guid userId, string code, CancellationToken ct = default)
    {
        if (!_recoveryCodeHashes.TryGetValue(userId, out var hashes)) return Task.FromResult(false);

        var totp = new TotpService();
        for (int i = 0; i < hashes.Count; i++)
        {
            if (totp.VerifyRecoveryCode(code, hashes[i]))
            {
                hashes.RemoveAt(i);
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    public Task<string> RegenerateInvitationTokenAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        if (_users.TryGetValue(userId, out var user))
        {
            var newToken = CryptoPasswordGenerator.GenerateInvitationToken();
            _users[userId] = user with { InvitationToken = newToken, MustChangePassword = true };
            return Task.FromResult(newToken);
        }

        return Task.FromResult(string.Empty);
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

    public Task<IReadOnlyList<MenuMasterDto>> GetMenusAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<MenuMasterDto>>(_menus.OrderBy(m => m.Position).ToList());
    }

    public Task<IReadOnlyList<GroupMenuMappingDto>> GetMenuMappingsAsync(Guid tenantId, Guid groupId, CancellationToken ct = default)
    {
        var list = new List<GroupMenuMappingDto>();
        if (_groups.TryGetValue(groupId, out var grp))
        {
            foreach (var menu in _menus)
            {
                var perm = grp.Permissions.FirstOrDefault(p => p.MenuId == menu.MenuCode);
                list.Add(new GroupMenuMappingDto(
                    groupId,
                    menu.Id,
                    menu.MenuName,
                    menu.MenuCode,
                    menu.Path,
                    perm?.CanView ?? false,
                    perm?.CanEdit ?? false,
                    perm?.CanAuthorize ?? false));
            }
        }
        return Task.FromResult<IReadOnlyList<GroupMenuMappingDto>>(list);
    }
}

public sealed class PostgreSqlIamUserService : IIamUserService
{
    private readonly string _connectionString;

    public PostgreSqlIamUserService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<IamUserDto>> GetUsersAsync(Guid tenantId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT id, tenant_id, group_id, username, COALESCE(full_name, username), role, 
                   COALESCE(department, 'Financial Crime Compliance'), is_active, 
                   COALESCE(mfa_enabled, false), COALESCE(shift_status, 'ON_DUTY'), 
                   created_at, COALESCE(must_change_password, true), invitation_token
            FROM core.users
            WHERE tenant_id = @tid
            ORDER BY created_at DESC;";

        var list = new List<IamUserDto>();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var created = r.GetDateTime(10).ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

            list.Add(new IamUserDto(
                r.GetGuid(0),
                r.GetGuid(1),
                r.IsDBNull(2) ? null : r.GetGuid(2),
                r.GetString(3),
                r.GetString(4),
                r.GetString(5),
                r.GetString(6),
                r.GetBoolean(7),
                r.GetBoolean(8),
                r.GetString(9),
                created,
                MustChangePassword: r.GetBoolean(11),
                InvitationToken: r.IsDBNull(12) ? null : r.GetString(12)));
        }

        return list;
    }

    public async Task<IamUserDto?> GetUserByIdAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT id, tenant_id, group_id, username, COALESCE(full_name, username), role, 
                   COALESCE(department, 'Financial Crime Compliance'), is_active, 
                   COALESCE(mfa_enabled, false), COALESCE(shift_status, 'ON_DUTY'), 
                   created_at, COALESCE(must_change_password, true), invitation_token
            FROM core.users
            WHERE id = @id AND tenant_id = @tid;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", userId);
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (await r.ReadAsync(ct))
        {
            var created = r.GetDateTime(10).ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
            return new IamUserDto(
                r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2),
                r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6),
                r.GetBoolean(7), r.GetBoolean(8), r.GetString(9), created,
                MustChangePassword: r.GetBoolean(11),
                InvitationToken: r.IsDBNull(12) ? null : r.GetString(12));
        }

        return null;
    }

    public async Task<IamUserDto?> GetUserByUsernameAsync(Guid tenantId, string username, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT id, tenant_id, group_id, username, COALESCE(full_name, username), role, 
                   COALESCE(department, 'Financial Crime Compliance'), is_active, 
                   COALESCE(mfa_enabled, false), COALESCE(shift_status, 'ON_DUTY'), 
                   created_at, COALESCE(must_change_password, true), invitation_token
            FROM core.users
            WHERE LOWER(username) = LOWER(@uname) AND tenant_id = @tid;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("uname", username.Trim());
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (await r.ReadAsync(ct))
        {
            var created = r.GetDateTime(10).ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
            return new IamUserDto(
                r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2),
                r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6),
                r.GetBoolean(7), r.GetBoolean(8), r.GetString(9), created,
                MustChangePassword: r.GetBoolean(11),
                InvitationToken: r.IsDBNull(12) ? null : r.GetString(12));
        }

        return null;
    }

    public async Task<IamUserDto?> GetUserByInvitationTokenAsync(string token, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT id, tenant_id, group_id, username, COALESCE(full_name, username), role, 
                   COALESCE(department, 'Financial Crime Compliance'), is_active, 
                   COALESCE(mfa_enabled, false), COALESCE(shift_status, 'ON_DUTY'), 
                   created_at, COALESCE(must_change_password, true), invitation_token
            FROM core.users
            WHERE invitation_token = @tok AND (invitation_token_expires_at IS NULL OR invitation_token_expires_at > NOW());";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tok", token.Trim());

        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (await r.ReadAsync(ct))
        {
            var created = r.GetDateTime(10).ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
            return new IamUserDto(
                r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2),
                r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6),
                r.GetBoolean(7), r.GetBoolean(8), r.GetString(9), created,
                MustChangePassword: r.GetBoolean(11),
                InvitationToken: r.IsDBNull(12) ? null : r.GetString(12));
        }

        return null;
    }

    public async Task<IamUserDto> CreateUserAsync(Guid tenantId, CreateIamUserRequest req, CancellationToken ct = default)
    {
        var (user, _, _) = await CreateUserWithTokenAsync(tenantId, req, ct);
        return user;
    }

    public async Task<(IamUserDto User, string RawPassword, string InvitationToken)> CreateUserWithTokenAsync(
        Guid tenantId, CreateIamUserRequest req, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var rawPass = CryptoPasswordGenerator.GenerateSecurePassword();
        var token = CryptoPasswordGenerator.GenerateInvitationToken();
        var passwordHash = IamPasswordHasher.HashPassword(rawPass);
        var fullName = !string.IsNullOrWhiteSpace(req.FullName) ? req.FullName.Trim() : req.Username.Trim();
        var role = !string.IsNullOrWhiteSpace(req.Role) ? req.Role.Trim().ToUpperInvariant() : "ANALYST";
        var dept = !string.IsNullOrWhiteSpace(req.Department) ? req.Department.Trim() : "Financial Crime Compliance";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            INSERT INTO core.users (
                id, tenant_id, group_id, username, full_name, role, department, 
                is_active, password_hash, must_change_password, mfa_enabled, 
                invitation_token, invitation_token_expires_at, created_at
            )
            VALUES (
                @id, @tid, @gid, @uname, @fname, @role, @dept, 
                TRUE, @phash, TRUE, FALSE, 
                @token, NOW() + INTERVAL '48 hours', NOW()
            )
            ON CONFLICT (tenant_id, username) DO UPDATE SET
                full_name = EXCLUDED.full_name,
                role = EXCLUDED.role,
                department = EXCLUDED.department,
                password_hash = EXCLUDED.password_hash,
                must_change_password = TRUE,
                invitation_token = EXCLUDED.invitation_token,
                invitation_token_expires_at = NOW() + INTERVAL '48 hours'
            RETURNING id;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("gid", (object?)req.GroupId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("uname", req.Username.Trim().ToLowerInvariant());
        cmd.Parameters.AddWithValue("fname", fullName);
        cmd.Parameters.AddWithValue("role", role);
        cmd.Parameters.AddWithValue("dept", dept);
        cmd.Parameters.AddWithValue("phash", passwordHash);
        cmd.Parameters.AddWithValue("token", token);
        var actualIdObj = await cmd.ExecuteScalarAsync(ct);
        if (actualIdObj is Guid actualGuid)
        {
            id = actualGuid;
        }

        var created = new IamUserDto(
            id, tenantId, req.GroupId, req.Username.Trim().ToLowerInvariant(),
            fullName, role, dept, true, false, "OFF_DUTY",
            DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture),
            MustChangePassword: true,
            InvitationToken: token);

        return (created, rawPass, token);
    }

    public async Task<IamUserDto?> UpdateUserAsync(Guid tenantId, Guid userId, UpdateIamUserRequest req, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sqlBuilder = new StringBuilder("UPDATE core.users SET ");
        var hasField = false;

        if (!string.IsNullOrWhiteSpace(req.FullName))
        {
            sqlBuilder.Append("full_name = @fn, ");
            hasField = true;
        }
        if (!string.IsNullOrWhiteSpace(req.Department))
        {
            sqlBuilder.Append("department = @dept, ");
            hasField = true;
        }
        if (!string.IsNullOrWhiteSpace(req.ShiftStatus))
        {
            sqlBuilder.Append("shift_status = @shift, ");
            hasField = true;
        }
        if (req.Role != null)
        {
            sqlBuilder.Append("role = @role, ");
            hasField = true;
        }
        if (req.GroupId != null)
        {
            sqlBuilder.Append("group_id = @gid, ");
            hasField = true;
        }
        if (req.IsActive.HasValue)
        {
            sqlBuilder.Append("is_active = @active, ");
            hasField = true;
        }
        if (req.MfaEnabled.HasValue)
        {
            if (req.MfaEnabled.Value == false)
            {
                sqlBuilder.Append("mfa_enabled = false, mfa_secret = NULL, mfa_enrolled_at = NULL, ");
            }
            else
            {
                sqlBuilder.Append("mfa_enabled = @mfa, ");
            }
            hasField = true;
        }
        if (req.MustChangePassword.HasValue)
        {
            sqlBuilder.Append("must_change_password = @mustChange, ");
            hasField = true;
        }
        if (!string.IsNullOrWhiteSpace(req.Password))
        {
            sqlBuilder.Append("password_hash = @phash, ");
            hasField = true;
        }

        if (hasField)
        {
            var sql = sqlBuilder.ToString().TrimEnd(' ', ',') + " WHERE id = @id AND tenant_id = @tid;";
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", userId);
            cmd.Parameters.AddWithValue("tid", tenantId);

            if (!string.IsNullOrWhiteSpace(req.FullName)) cmd.Parameters.AddWithValue("fn", req.FullName.Trim());
            if (!string.IsNullOrWhiteSpace(req.Department)) cmd.Parameters.AddWithValue("dept", req.Department.Trim());
            if (!string.IsNullOrWhiteSpace(req.ShiftStatus)) cmd.Parameters.AddWithValue("shift", req.ShiftStatus.Trim().ToUpperInvariant());
            if (req.Role != null) cmd.Parameters.AddWithValue("role", req.Role.Trim().ToUpperInvariant());
            if (req.GroupId != null) cmd.Parameters.AddWithValue("gid", req.GroupId.Value);
            if (req.IsActive.HasValue) cmd.Parameters.AddWithValue("active", req.IsActive.Value);
            if (req.MfaEnabled.HasValue && req.MfaEnabled.Value) cmd.Parameters.AddWithValue("mfa", true);
            if (req.MustChangePassword.HasValue) cmd.Parameters.AddWithValue("mustChange", req.MustChangePassword.Value);
            if (!string.IsNullOrWhiteSpace(req.Password)) cmd.Parameters.AddWithValue("phash", IamPasswordHasher.HashPassword(req.Password));

            await cmd.ExecuteNonQueryAsync(ct);
        }

        return await GetUserByIdAsync(tenantId, userId, ct);
    }

    public async Task<bool> DeleteUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = "DELETE FROM core.users WHERE id = @id AND tenant_id = @tid;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", userId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<bool> VerifyPasswordAsync(Guid tenantId, string username, string password, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = "SELECT password_hash FROM core.users WHERE LOWER(username) = LOWER(@uname) AND tenant_id = @tid;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("uname", username.Trim());
        cmd.Parameters.AddWithValue("tid", tenantId);

        var hashObj = await cmd.ExecuteScalarAsync(ct);
        if (hashObj is string hash)
        {
            return IamPasswordHasher.VerifyPassword(password, hash);
        }

        return false;
    }

    public async Task<Guid?> FindTenantIdByUsernameAsync(string username, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = "SELECT tenant_id FROM core.users WHERE LOWER(username) = LOWER(@uname) LIMIT 1;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("uname", username.Trim().ToLowerInvariant());

        var res = await cmd.ExecuteScalarAsync(ct);
        return res is Guid g ? g : null;
    }

    public async Task<string?> GetMfaSecretAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = "SELECT mfa_secret FROM core.users WHERE id = @id AND tenant_id = @tid;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", userId);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var res = await cmd.ExecuteScalarAsync(ct);
        return res is string s && !string.IsNullOrWhiteSpace(s) ? s : null;
    }

    public async Task<bool> CompleteOnboardingAsync(
        string token, string newPassword, string mfaSecret, IReadOnlyList<string> recoveryCodes, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var newHash = IamPasswordHasher.HashPassword(newPassword);

        const string getSql = "SELECT id, tenant_id FROM core.users WHERE invitation_token = @tok;";
        Guid userId;
        await using (var getCmd = new NpgsqlCommand(getSql, conn))
        {
            getCmd.Parameters.AddWithValue("tok", token.Trim());
            await using var r = await getCmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return false;
            userId = r.GetGuid(0);
        }

        const string updSql = @"
            UPDATE core.users
            SET password_hash = @phash,
                must_change_password = FALSE,
                mfa_enabled = TRUE,
                mfa_secret = @sec,
                mfa_enrolled_at = NOW(),
                invitation_token = NULL,
                invitation_token_expires_at = NULL,
                last_login_at = NOW()
            WHERE id = @id;";

        await using (var updCmd = new NpgsqlCommand(updSql, conn))
        {
            updCmd.Parameters.AddWithValue("phash", newHash);
            updCmd.Parameters.AddWithValue("sec", mfaSecret);
            updCmd.Parameters.AddWithValue("id", userId);
            await updCmd.ExecuteNonQueryAsync(ct);
        }

        // Insert recovery codes
        var totp = new TotpService();
        foreach (var code in recoveryCodes)
        {
            const string codeSql = @"
                INSERT INTO core.user_recovery_codes (id, user_id, code_hash, created_at)
                VALUES (@id, @uid, @chash, NOW());";

            await using var codeCmd = new NpgsqlCommand(codeSql, conn);
            codeCmd.Parameters.AddWithValue("id", Guid.NewGuid());
            codeCmd.Parameters.AddWithValue("uid", userId);
            codeCmd.Parameters.AddWithValue("chash", totp.HashRecoveryCode(code));
            await codeCmd.ExecuteNonQueryAsync(ct);
        }

        return true;
    }

    public async Task<bool> VerifyAndConsumeRecoveryCodeAsync(Guid tenantId, Guid userId, string code, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = "SELECT id, code_hash FROM core.user_recovery_codes WHERE user_id = @uid AND used_at IS NULL;";
        var list = new List<(Guid Id, string Hash)>();

        await using (var cmd = new NpgsqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("uid", userId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                list.Add((r.GetGuid(0), r.GetString(1)));
            }
        }

        var totp = new TotpService();
        foreach (var (codeId, storedHash) in list)
        {
            if (totp.VerifyRecoveryCode(code, storedHash))
            {
                const string markSql = "UPDATE core.user_recovery_codes SET used_at = NOW() WHERE id = @id;";
                await using var markCmd = new NpgsqlCommand(markSql, conn);
                markCmd.Parameters.AddWithValue("id", codeId);
                await markCmd.ExecuteNonQueryAsync(ct);
                return true;
            }
        }

        return false;
    }

    public async Task<string> RegenerateInvitationTokenAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var token = CryptoPasswordGenerator.GenerateInvitationToken();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            UPDATE core.users
            SET invitation_token = @tok,
                invitation_token_expires_at = NOW() + INTERVAL '48 hours',
                must_change_password = TRUE
            WHERE id = @id AND tenant_id = @tid;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tok", token);
        cmd.Parameters.AddWithValue("id", userId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        await cmd.ExecuteNonQueryAsync(ct);
        return token;
    }

    public async Task<IReadOnlyList<IamGroupDto>> GetGroupsAsync(Guid tenantId, CancellationToken ct = default)
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

        var result = new List<IamGroupDto>();
        foreach (var g in groups)
        {
            const string permSql = @"
                SELECT m.menu_code, p.can_view, p.can_edit, p.can_authorize
                FROM core.group_menu_permissions p
                JOIN core.menus m ON p.menu_id = m.id
                WHERE p.group_id = @gid;";

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

    public async Task<IamGroupDto> CreateGroupAsync(Guid tenantId, CreateIamGroupRequest req, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var name = req.GroupName.Trim().ToUpperInvariant();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            INSERT INTO core.user_groups (id, tenant_id, group_name, is_active)
            VALUES (@id, @tid, @name, TRUE)
            ON CONFLICT (id) DO NOTHING;";

        await using (var cmd = new NpgsqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("name", name);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        return new IamGroupDto(id, tenantId, name, true, req.Permissions ?? []);
    }

    public async Task<IamGroupDto?> UpdateGroupPermissionsAsync(Guid tenantId, Guid groupId, UpdateGroupPermissionsRequest req, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var tx = await conn.BeginTransactionAsync(ct);

        // Clear existing permissions for this group
        const string delSql = "DELETE FROM core.group_menu_permissions WHERE group_id = @gid;";
        await using (var delCmd = new NpgsqlCommand(delSql, conn, tx))
        {
            delCmd.Parameters.AddWithValue("gid", groupId);
            await delCmd.ExecuteNonQueryAsync(ct);
        }

        // Insert updated permissions
        const string insSql = @"
            INSERT INTO core.group_menu_permissions (id, group_id, menu_id, can_view, can_edit, can_authorize)
            SELECT gen_random_uuid(), @gid, m.id, @view, @edit, @auth
            FROM core.menus m
            WHERE m.menu_code = @code OR m.id::text = @code;";

        foreach (var perm in req.Permissions)
        {
            await using var insCmd = new NpgsqlCommand(insSql, conn, tx);
            insCmd.Parameters.AddWithValue("gid", groupId);
            insCmd.Parameters.AddWithValue("code", perm.MenuId);
            insCmd.Parameters.AddWithValue("view", perm.CanView);
            insCmd.Parameters.AddWithValue("edit", perm.CanEdit);
            insCmd.Parameters.AddWithValue("auth", perm.CanAuthorize);
            await insCmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);

        var groups = await GetGroupsAsync(tenantId, ct);
        return groups.FirstOrDefault(g => g.Id == groupId);
    }

    public async Task<IReadOnlyList<MenuMasterDto>> GetMenusAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT id, parent_menu_id, menu_name, COALESCE(menu_code, menu_name), path, position, COALESCE(icon, 'Database'), COALESCE(is_active, true)
            FROM core.menus
            ORDER BY position;";

        var list = new List<MenuMasterDto>();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new MenuMasterDto(
                r.GetGuid(0),
                r.IsDBNull(1) ? null : r.GetGuid(1),
                r.GetString(2),
                r.GetString(3),
                r.GetString(4),
                r.GetInt32(5),
                r.GetString(6),
                r.GetBoolean(7)));
        }

        return list;
    }

    public async Task<IReadOnlyList<GroupMenuMappingDto>> GetMenuMappingsAsync(Guid tenantId, Guid groupId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT m.id, m.menu_name, COALESCE(m.menu_code, m.menu_name), COALESCE(m.path, ''),
                   COALESCE(p.can_view, false),
                   COALESCE(p.can_edit, false),
                   COALESCE(p.can_authorize, false)
            FROM core.menus m
            LEFT JOIN core.group_menu_permissions p ON p.menu_id = m.id AND p.group_id = @gid
            ORDER BY m.position;";

        var list = new List<GroupMenuMappingDto>();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("gid", groupId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new GroupMenuMappingDto(
                groupId,
                r.GetGuid(0),
                r.GetString(1),
                r.GetString(2),
                r.GetString(3),
                r.GetBoolean(4),
                r.GetBoolean(5),
                r.GetBoolean(6)));
        }

        return list;
    }
}
