using System.ComponentModel.DataAnnotations;

namespace Edith.Gateway.Models;

public static class EdithRoles
{
    public const string SuperAdmin = "SUPER_ADMIN";
    public const string ComplianceOfficer = "COMPLIANCE_OFFICER";
    public const string Mlro = "MLRO";
    public const string Analyst = "ANALYST";
    public const string Auditor = "AUDITOR";

    public static readonly IReadOnlyList<string> All =
    [
        SuperAdmin,
        ComplianceOfficer,
        Mlro,
        Analyst,
        Auditor
    ];
}

public sealed record TokenRequest
{
    [Required]
    public required string Username { get; init; }

    [Required]
    public required string Password { get; init; }

    public Guid? TenantId { get; init; }

    public string? Role { get; init; }
}

public sealed record TokenResponse
{
    public required string AccessToken { get; init; }
    public required string TokenType { get; init; }
    public required int ExpiresIn { get; init; }
    public required Guid TenantId { get; init; }
    public required string Role { get; init; }
}

public sealed record UserEntitlementsResponse
{
    public required Guid UserId { get; init; }
    public required string Username { get; init; }
    public required string FullName { get; init; }
    public required Guid TenantId { get; init; }
    public required string TenantName { get; init; }
    public required string Role { get; init; }
    public required IReadOnlyList<string> Permissions { get; init; }
    public required IReadOnlyList<MenuEntitlementDto> Menus { get; init; }
}

public sealed record MenuEntitlementDto
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required string Icon { get; init; }
    public required string Route { get; init; }
    public required int OrderIndex { get; init; }
    public required IReadOnlyList<string> AllowedActions { get; init; }
}
