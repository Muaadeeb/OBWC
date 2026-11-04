using System.ComponentModel.DataAnnotations;

namespace OBWC.Shared.Dtos;

public record RegisterRequest
{
    [Required(ErrorMessage = "Enter your email."), EmailAddress(ErrorMessage = "Enter a valid email address."), MaxLength(256)]
    public string Email { get; init; } = "";

    [Required(ErrorMessage = "Enter a password."), MinLength(8, ErrorMessage = "Use at least 8 characters."), MaxLength(128)]
    public string Password { get; init; } = "";

    [Required(ErrorMessage = "Enter the name other members should see."), MaxLength(100)]
    public string DisplayName { get; init; } = "";

    public MembershipType? MembershipType { get; init; }
}

public record LoginRequest
{
    [Required(ErrorMessage = "Enter your email."), EmailAddress(ErrorMessage = "Enter a valid email address."), MaxLength(256)]
    public string Email { get; init; } = "";

    [Required(ErrorMessage = "Enter your password."), MaxLength(128)]
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
    [Required(ErrorMessage = "Enter your email."), EmailAddress(ErrorMessage = "Enter a valid email address."), MaxLength(256)]
    public string Email { get; init; } = "";
}

public record ResetPasswordRequest
{
    [Required]
    public string UserId { get; init; } = "";

    [Required]
    public string Token { get; init; } = "";

    [Required(ErrorMessage = "Enter a new password."), MinLength(8, ErrorMessage = "Use at least 8 characters."), MaxLength(128)]
    public string NewPassword { get; init; } = "";
}

public record ChangePasswordRequest
{
    [Required(ErrorMessage = "Enter your current password.")]
    public string CurrentPassword { get; init; } = "";

    [Required(ErrorMessage = "Enter a new password."), MinLength(8, ErrorMessage = "Use at least 8 characters."), MaxLength(128)]
    public string NewPassword { get; init; } = "";
}

public record UpdateProfileRequest
{
    [Required(ErrorMessage = "Enter the name other members should see."), MaxLength(100)]
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
