using Edith.Gateway.Models;

namespace Edith.Gateway.Services;

public interface IEntitlementsService
{
    UserEntitlementsResponse GetEntitlements(Guid userId, string username, string role, TenantContext tenant);
}

public sealed class EntitlementsService : IEntitlementsService
{
    public UserEntitlementsResponse GetEntitlements(Guid userId, string username, string role, TenantContext tenant)
    {
        var normalizedRole = role.ToUpperInvariant();
        var (permissions, menus) = BuildEntitlements(normalizedRole);

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

    private static (List<string> Permissions, List<MenuEntitlementDto> Menus) BuildEntitlements(string role)
    {
        var permissions = new List<string>();
        var menus = new List<MenuEntitlementDto>();

        // 1. Dashboard & Name Search (Accessible by ALL roles)
        menus.Add(new MenuEntitlementDto
        {
            Key = "dashboard",
            Label = "Executive Cockpit",
            Icon = "LayoutDashboard",
            Route = "/dashboard",
            OrderIndex = 1,
            AllowedActions = ["view", "filter"]
        });
        menus.Add(new MenuEntitlementDto
        {
            Key = "name_search",
            Label = "Name & ID Search",
            Icon = "Search",
            Route = "/name-search",
            OrderIndex = 2,
            AllowedActions = ["search", "view"]
        });
        permissions.Add("dashboard:view");
        permissions.Add("screening:name_search");

        switch (role)
        {
            case EdithRoles.SuperAdmin:
                permissions.AddRange([
                    "screening:execute", "screening:view", "screening:bulk",
                    "cases:view", "cases:investigate", "cases:lock", "cases:dispose", "cases:freeze", "cases:str_export",
                    "rules:view", "rules:create", "rules:edit", "rules:simulate",
                    "whitelists:view", "whitelists:create", "whitelists:delete",
                    "admin:masters", "admin:tenants", "audit:view", "audit:export"
                ]);

                menus.AddRange([
                    new MenuEntitlementDto { Key = "screening", Label = "Real-time Screening", Icon = "Search", Route = "/screening", OrderIndex = 2, AllowedActions = ["screen", "view"] },
                    new MenuEntitlementDto { Key = "screening_bulk", Label = "Bulk Screening Hub", Icon = "FileSpreadsheet", Route = "/screening/bulk", OrderIndex = 3, AllowedActions = ["upload", "process", "download"] },
                    new MenuEntitlementDto { Key = "cases", Label = "Case Investigation Canvas", Icon = "Briefcase", Route = "/cases", OrderIndex = 4, AllowedActions = ["view", "investigate", "lock", "dispose", "freeze", "str_export"] },
                    new MenuEntitlementDto { Key = "rules", Label = "AML Rule Builder & Sandbox", Icon = "Sliders", Route = "/rules", OrderIndex = 5, AllowedActions = ["view", "create", "edit", "simulate"] },
                    new MenuEntitlementDto { Key = "whitelists", Label = "Whitelist Governance", Icon = "ShieldCheck", Route = "/whitelists", OrderIndex = 6, AllowedActions = ["view", "create", "delete"] },
                    new MenuEntitlementDto { Key = "masters", Label = "System Masters & Tenancy", Icon = "Settings", Route = "/admin/masters", OrderIndex = 7, AllowedActions = ["manage"] },
                    new MenuEntitlementDto { Key = "audit", Label = "Immutable Audit Ledger", Icon = "FileText", Route = "/audit", OrderIndex = 8, AllowedActions = ["view", "export"] }
                ]);
                break;

            case EdithRoles.Mlro:
                permissions.AddRange([
                    "screening:execute", "screening:view", "screening:bulk",
                    "cases:view", "cases:investigate", "cases:lock", "cases:dispose", "cases:freeze", "cases:str_export",
                    "rules:view", "rules:simulate",
                    "whitelists:view", "whitelists:create", "whitelists:delete",
                    "audit:view", "audit:export"
                ]);

                menus.AddRange([
                    new MenuEntitlementDto { Key = "screening", Label = "Real-time Screening", Icon = "Search", Route = "/screening", OrderIndex = 2, AllowedActions = ["screen", "view"] },
                    new MenuEntitlementDto { Key = "cases", Label = "Case Investigation Canvas", Icon = "Briefcase", Route = "/cases", OrderIndex = 4, AllowedActions = ["view", "investigate", "lock", "dispose", "freeze", "str_export"] },
                    new MenuEntitlementDto { Key = "whitelists", Label = "Whitelist Governance", Icon = "ShieldCheck", Route = "/whitelists", OrderIndex = 6, AllowedActions = ["view", "create", "delete"] },
                    new MenuEntitlementDto { Key = "audit", Label = "Immutable Audit Ledger", Icon = "FileText", Route = "/audit", OrderIndex = 8, AllowedActions = ["view", "export"] }
                ]);
                break;

            case EdithRoles.ComplianceOfficer:
                permissions.AddRange([
                    "screening:execute", "screening:view", "screening:bulk",
                    "cases:view", "cases:investigate", "cases:lock", "cases:dispose", "cases:freeze",
                    "rules:view", "rules:create", "rules:edit", "rules:simulate",
                    "whitelists:view", "whitelists:create",
                    "audit:view"
                ]);

                menus.AddRange([
                    new MenuEntitlementDto { Key = "screening", Label = "Real-time Screening", Icon = "Search", Route = "/screening", OrderIndex = 2, AllowedActions = ["screen", "view"] },
                    new MenuEntitlementDto { Key = "screening_bulk", Label = "Bulk Screening Hub", Icon = "FileSpreadsheet", Route = "/screening/bulk", OrderIndex = 3, AllowedActions = ["upload", "process"] },
                    new MenuEntitlementDto { Key = "cases", Label = "Case Investigation Canvas", Icon = "Briefcase", Route = "/cases", OrderIndex = 4, AllowedActions = ["view", "investigate", "lock", "dispose"] },
                    new MenuEntitlementDto { Key = "rules", Label = "AML Rule Builder & Sandbox", Icon = "Sliders", Route = "/rules", OrderIndex = 5, AllowedActions = ["view", "create", "edit", "simulate"] },
                    new MenuEntitlementDto { Key = "whitelists", Label = "Whitelist Governance", Icon = "ShieldCheck", Route = "/whitelists", OrderIndex = 6, AllowedActions = ["view", "create"] }
                ]);
                break;

            case EdithRoles.Analyst:
                permissions.AddRange([
                    "screening:execute", "screening:view",
                    "cases:view", "cases:investigate", "cases:lock",
                    "whitelists:view"
                ]);

                menus.AddRange([
                    new MenuEntitlementDto { Key = "screening", Label = "Real-time Screening", Icon = "Search", Route = "/screening", OrderIndex = 2, AllowedActions = ["screen", "view"] },
                    new MenuEntitlementDto { Key = "cases", Label = "Case Investigation Canvas", Icon = "Briefcase", Route = "/cases", OrderIndex = 4, AllowedActions = ["view", "investigate", "lock", "recommend"] },
                    new MenuEntitlementDto { Key = "whitelists", Label = "Whitelist Governance", Icon = "ShieldCheck", Route = "/whitelists", OrderIndex = 6, AllowedActions = ["view"] }
                ]);
                break;

            case EdithRoles.Auditor:
                permissions.AddRange([
                    "cases:view",
                    "audit:view", "audit:export"
                ]);

                menus.AddRange([
                    new MenuEntitlementDto { Key = "cases", Label = "Case Records (Read-Only)", Icon = "Briefcase", Route = "/cases", OrderIndex = 4, AllowedActions = ["view"] },
                    new MenuEntitlementDto { Key = "audit", Label = "Immutable Audit Ledger", Icon = "FileText", Route = "/audit", OrderIndex = 8, AllowedActions = ["view", "export"] }
                ]);
                break;
        }

        return (permissions, menus.OrderBy(m => m.OrderIndex).ToList());
    }

    private static string FormatDisplayName(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return "EDITH User";
        var parts = username.Split(['.', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant()));
    }
}
