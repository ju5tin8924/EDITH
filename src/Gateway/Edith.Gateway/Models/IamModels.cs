using System.ComponentModel.DataAnnotations;

namespace Edith.Gateway.Models;

public static class EdithRoles
{
    public const string SuperAdmin = "SUPER_ADMIN";
    public const string Admin = "ADMIN";
    public const string AmlOfficer = "AML_OFFICER";
    public const string EddAnalyst = "EDD_ANALYST";
    public const string IstrMlro = "ISTR_MLRO";

    // Legacy / Convenience Aliases
    public const string ComplianceOfficer = "COMPLIANCE_OFFICER";
    public const string Mlro = "MLRO";
    public const string Analyst = "ANALYST";
    public const string Auditor = "AUDITOR";

    public static readonly IReadOnlyList<string> All =
    [
        SuperAdmin,
        Admin,
        AmlOfficer,
        EddAnalyst,
        IstrMlro,
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

public sealed record LoginRequest(
    string Username,
    string Password,
    Guid? TenantId = null
);

public sealed record LoginResponse(
    string Status, // SUCCESS, MFA_REQUIRED, ONBOARDING_REQUIRED
    string? AccessToken,
    string? MfaSessionToken,
    string? OnboardingToken,
    string? Role,
    Guid? TenantId,
    string? Message
);

public sealed record MfaVerifyRequest(
    string MfaSessionToken,
    string Code
);

public sealed record OnboardingValidateResponse(
    bool Valid,
    string Username,
    string FullName,
    string Role,
    string TenantName,
    string? Error = null
);

public sealed record MfaSetupResponse(
    string SecretBase32,
    string OtpAuthUri,
    string Username
);

public sealed record OnboardingCompleteRequest(
    string Token,
    string TemporaryPassword,
    string NewPassword,
    string TotpCode
);

public sealed record OnboardingCompleteResponse(
    bool Success,
    string? AccessToken,
    IReadOnlyList<string>? RecoveryCodes,
    string Message
);

public sealed record CreateIamUserResponse(
    Services.IamUserDto User,
    string? GeneratedTemporaryPassword,
    string EmailStatus, // SENT, FAILED, SKIPPED
    string? EmailError,
    string? InvitationToken = null
);

public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);

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
    public IReadOnlyList<MenuEntitlementDto>? SubMenus { get; init; }
}
