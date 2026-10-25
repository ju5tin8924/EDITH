using Edith.Gateway.Models;

namespace Edith.Gateway.Services;

public interface IEntitlementsService
{
    Task<UserEntitlementsResponse> GetEntitlementsAsync(Guid userId, string username, string role, TenantContext tenant, CancellationToken ct = default);
    UserEntitlementsResponse GetEntitlements(Guid userId, string username, string role, TenantContext tenant);
}

public sealed class EntitlementsService : IEntitlementsService
{
    private readonly IIamUserService _iamService;

    public EntitlementsService(IIamUserService iamService)
    {
        _iamService = iamService;
    }

    public async Task<UserEntitlementsResponse> GetEntitlementsAsync(Guid userId, string username, string role, TenantContext tenant, CancellationToken ct = default)
    {
        var normalizedRole = role.ToUpperInvariant();
        var (permissions, menus) = await BuildDatabaseEntitlementsAsync(userId, username, normalizedRole, tenant.TenantId, ct);

        return new UserEntitlementsResponse
        {
            UserId = userId,
            Username = username,
            FullName = FormatDisplayName(username),
            TenantId = tenant.TenantId,
            TenantName = tenant.TenantName,
            Role = normalizedRole,
            Permissions = permissions,
            Menus = menus
        };
    }

    public UserEntitlementsResponse GetEntitlements(Guid userId, string username, string role, TenantContext tenant)
    {
        return GetEntitlementsAsync(userId, username, role, tenant, CancellationToken.None).GetAwaiter().GetResult();
    }

    private async Task<(List<string> Permissions, List<MenuEntitlementDto> Menus)> BuildDatabaseEntitlementsAsync(
        Guid userId, string username, string role, Guid tenantId, CancellationToken ct)
    {
        var permissions = new List<string> { "dashboard:view", "screening:name_search" };
        var isAdmin = role is EdithRoles.SuperAdmin or EdithRoles.Admin;
        var isMlro = role is EdithRoles.IstrMlro or EdithRoles.Mlro;
        var isAnalyst = role is EdithRoles.EddAnalyst or EdithRoles.Analyst;

        // 1. Fetch user to check assigned group
        var user = await _iamService.GetUserByUsernameAsync(tenantId, username, ct) 
                ?? await _iamService.GetUserByIdAsync(tenantId, userId, ct);

        // 2. Query all menus from PostgreSQL (core.menus)
        var allMenusFromDb = await _iamService.GetMenusAsync(ct);

        // 3. Query group permissions if user has a group assigned
        IReadOnlyList<GroupMenuMappingDto> groupMappings = [];
        if (user?.GroupId != null)
        {
            groupMappings = await _iamService.GetMenuMappingsAsync(tenantId, user.GroupId.Value, ct);
        }

        var visibleMenuCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var authMenuCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var editMenuCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (isAdmin)
        {
            // Admins have access to all menus
            foreach (var m in allMenusFromDb) visibleMenuCodes.Add(m.MenuCode);
            permissions.AddRange([
                "settings:manage", "users:manage", "groups:manage", "masters:manage",
                "screening:execute", "screening:view", "screening:bulk", "cases:freeze", "cases:investigate"
            ]);
        }
        else if (groupMappings.Count > 0)
        {
            foreach (var mapping in groupMappings)
            {
                if (mapping.CanView) visibleMenuCodes.Add(mapping.MenuCode);
                if (mapping.CanAuthorize) authMenuCodes.Add(mapping.MenuCode);
                if (mapping.CanEdit) editMenuCodes.Add(mapping.MenuCode);
            }

            if (isMlro)
            {
                permissions.AddRange([
                    "str:mlro_supervise", "cases:freeze", "cases:unfreeze", "aml:monitoring",
                    "reports:view", "reports:export", "log:view", "customer:kyc"
                ]);
            }
            else if (isAnalyst)
            {
                permissions.AddRange([
                    "str:edd_investigate", "customer:kyc", "customer:screening", "whitelist:view"
                ]);
            }
            else
            {
                permissions.AddRange([
                    "authorization:execute", "str:alerts", "aml:rules", "aml:process",
                    "blacklist:screen", "whitelist:manage", "delta:authorize"
                ]);
            }
        }
        else
        {
            // Role fallback matching user permissions spec
            AssignRoleFallback(role, visibleMenuCodes, permissions);
        }

        // Always ensure Home / Dashboard is visible
        visibleMenuCodes.Add("MENU_DASHBOARD");

        // 4. Build hierarchical tree: parent menus + sub-menus
        var parentMenus = allMenusFromDb.Where(m => m.ParentMenuId == null).OrderBy(m => m.Position).ToList();
        var subMenus = allMenusFromDb.Where(m => m.ParentMenuId != null).OrderBy(m => m.Position).ToList();

        var resultMenus = new List<MenuEntitlementDto>();
        foreach (var parent in parentMenus)
        {
            if (!visibleMenuCodes.Contains(parent.MenuCode))
            {
                continue;
            }

            // Find child sub-menus
            var children = subMenus.Where(s => s.ParentMenuId == parent.Id).ToList();
            List<MenuEntitlementDto>? mappedChildren = null;

            if (children.Count > 0)
            {
                mappedChildren = children.Select(c => new MenuEntitlementDto
                {
                    Key = c.MenuCode,
                    Label = c.MenuName,
                    Icon = c.Icon,
                    Route = c.Path,
                    OrderIndex = c.Position,
                    AllowedActions = DetermineAllowedActions(role, c.MenuCode, authMenuCodes.Contains(c.MenuCode), editMenuCodes.Contains(c.MenuCode))
                }).ToList();
            }

            resultMenus.Add(new MenuEntitlementDto
            {
                Key = parent.MenuCode,
                Label = parent.MenuName,
                Icon = parent.Icon,
                Route = parent.Path,
                OrderIndex = parent.Position,
                AllowedActions = DetermineAllowedActions(role, parent.MenuCode, authMenuCodes.Contains(parent.MenuCode), editMenuCodes.Contains(parent.MenuCode)),
                SubMenus = mappedChildren
            });
        }

        return (permissions.Distinct().ToList(), resultMenus.OrderBy(m => m.OrderIndex).ToList());
    }

    private static void AssignRoleFallback(string role, HashSet<string> visibleMenuCodes, List<string> permissions)
    {
        switch (role)
        {
            case EdithRoles.AmlOfficer:
            case EdithRoles.ComplianceOfficer:
                permissions.AddRange(["authorization:execute", "str:alerts", "aml:rules", "aml:process", "blacklist:screen", "whitelist:manage", "delta:authorize"]);
                foreach (var code in new[] { "MENU_DASHBOARD", "MENU_AUTHORIZATION", "MENU_STR_PROCESS", "MENU_AML", "MENU_BLACKLIST", "MENU_WHITELIST", "MENU_DELTA" })
                    visibleMenuCodes.Add(code);
                break;

            case EdithRoles.EddAnalyst:
            case EdithRoles.Analyst:
                permissions.AddRange(["str:edd_investigate", "customer:kyc", "customer:screening", "whitelist:view"]);
                foreach (var code in new[] { "MENU_DASHBOARD", "MENU_STR_PROCESS", "MENU_CUSTOMER", "MENU_WHITELIST" })
                    visibleMenuCodes.Add(code);
                break;

            case EdithRoles.IstrMlro:
            case EdithRoles.Mlro:
                permissions.AddRange(["str:mlro_supervise", "cases:freeze", "cases:unfreeze", "aml:monitoring", "reports:view", "reports:export", "log:view", "customer:kyc"]);
                foreach (var code in new[] { "MENU_DASHBOARD", "MENU_STR_PROCESS", "MENU_AML", "MENU_REPORTS", "MENU_CUSTOMER" })
                    visibleMenuCodes.Add(code);
                break;

            case EdithRoles.Auditor:
                permissions.AddRange(["reports:view", "log:view", "audit:view"]);
                foreach (var code in new[] { "MENU_DASHBOARD", "MENU_REPORTS" })
                    visibleMenuCodes.Add(code);
                break;

            default:
                foreach (var code in new[] { "MENU_DASHBOARD", "MENU_BLACKLIST", "MENU_CUSTOMER" })
                    visibleMenuCodes.Add(code);
                break;
        }
    }

    private static List<string> DetermineAllowedActions(string role, string menuCode, bool canAuthorize, bool canEdit)
    {
        var actions = new List<string> { "view", "filter" };

        if (role is EdithRoles.SuperAdmin or EdithRoles.Admin)
        {
            return ["view", "filter", "edit", "create", "manage", "authorize", "freeze", "unfreeze", "dispose", "str_export"];
        }

        if (role is EdithRoles.IstrMlro or EdithRoles.Mlro)
        {
            if (menuCode is "MENU_STR_PROCESS" or "cases")
            {
                return ["view", "filter", "dispose", "freeze", "unfreeze", "str_export", "escalate"];
            }
            return ["view", "filter", "authorize", "freeze", "unfreeze"];
        }

        if (role is EdithRoles.EddAnalyst or EdithRoles.Analyst)
        {
            if (menuCode is "MENU_STR_PROCESS" or "cases")
            {
                return ["view", "filter", "recommend", "investigate", "classify"];
            }
            return ["view", "filter", "investigate"];
        }

        if (canAuthorize) actions.AddRange(["authorize", "release", "reject"]);
        if (canEdit) actions.AddRange(["edit", "create", "dispose"]);

        return actions.Distinct().ToList();
    }

    private static string FormatDisplayName(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return "EDITH Officer";
        var namePart = username.Split('@')[0];
        var parts = namePart.Split(['.', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant()));
    }
}
