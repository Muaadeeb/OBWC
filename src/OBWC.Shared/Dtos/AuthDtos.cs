using System.ComponentModel.DataAnnotations;

namespace OBWC.Shared.Dtos;

public record RegisterRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = "";

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; init; } = "";

    [Required, MaxLength(100)]
    public string DisplayName { get; init; } = "";

    public MembershipType? MembershipType { get; init; }
}

public record LoginRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = "";

    [Required, MaxLength(128)]
    public string Password { get; init; } = "";
}

public record RefreshRequest
{
    [Required]
    public string RefreshToken { get; init; } = "";
}

public record ConfirmEmailRequest
{
    [Required]
    public string UserId { get; init; } = "";

    [Required]
    public string Token { get; init; } = "";
}

public record ForgotPasswordRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = "";
}

public record ResetPasswordRequest
{
    [Required]
    public string UserId { get; init; } = "";

    [Required]
    public string Token { get; init; } = "";

    [Required, MinLength(8), MaxLength(128)]
    public string NewPassword { get; init; } = "";
}

public record ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; init; } = "";

    [Required, MinLength(8), MaxLength(128)]
    public string NewPassword { get; init; } = "";
}

public record UpdateProfileRequest
{
    [Required, MaxLength(100)]
    public string DisplayName { get; init; } = "";

    public MembershipType? MembershipType { get; init; }
}

public record AuthResponse
{
    public string AccessToken { get; init; } = "";
    public string RefreshToken { get; init; } = "";
    public DateTime AccessTokenExpiresAtUtc { get; init; }
    public CurrentUserDto User { get; init; } = new();
}

public record MessageResponse
{
    public string Message { get; init; } = "";
}

public record CurrentUserDto
{
    public string Id { get; init; } = "";
    public string Email { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public MembershipType? MembershipType { get; init; }
    public bool EmailConfirmed { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
}

public record UserSummaryDto
{
    public string Id { get; init; } = "";
    public string Email { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public MembershipType? MembershipType { get; init; }
    public bool EmailConfirmed { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
}

public record UpdateRolesRequest
{
    public bool Member { get; init; } = true;
    public bool Admin { get; init; }
    public bool Gm { get; init; }

    /// <summary>
    /// Always rejected. SuperAdmin is applied only from Authorization:SuperAdminEmail.
    /// </summary>
    public bool SuperAdmin { get; init; }
}
