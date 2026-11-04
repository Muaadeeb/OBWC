namespace OBWC.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<ApplicationUser> users,
    TokenService tokens,
    IClubMailer mail,
    SuperAdminPromoter promoter,
    ScheduleService schedule,
    IOptions<AppOptions> appOptions) : ClubApiController
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<MessageResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var displayName = request.DisplayName.Trim();
        if (displayName.Length == 0)
            return Bad("Enter the name you want other members to see.");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
            MembershipType = request.MembershipType
        };
        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            return IdentityProblem(created);

        var member = await users.AddToRoleAsync(user, RoleNames.Member);
        if (!member.Succeeded)
            return IdentityProblem(member);

        await promoter.PromoteAsync(user);
        await schedule.SeedMemberOnUpcomingSessionsAsync(user.Id, cancellationToken);
        await SendConfirmationAsync(user);
        return Accepted(new MessageResponse
        {
            Message = "Account created. Check your email to confirm it before you sign in. In Development, when SMTP is not configured, the confirmation link is in the API log and on the Dev mail page."
        });
    }

    [HttpPost("confirm-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request)
    {
        var user = await users.FindByIdAsync(request.UserId);
        if (user is null)
            return Bad("This confirmation link is not valid.");

        var result = await users.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded)
            return IdentityProblem(result);

        await promoter.PromoteAsync(user);
        await schedule.SeedMemberOnUpcomingSessionsAsync(user.Id);
        return NoContent();
    }

    [HttpPost("resend-confirmation")]
    [AllowAnonymous]
    public async Task<ActionResult<MessageResponse>> ResendConfirmation(ForgotPasswordRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is not null && !user.EmailConfirmed)
            await SendConfirmationAsync(user);
        return Accepted(new MessageResponse { Message = "If that account exists and is not confirmed yet, a new confirmation email is on its way." });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
            return Problem(detail: "Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized");

        if (await users.IsLockedOutAsync(user))
            return Problem(detail: "This account is temporarily locked. Try again later.", statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized");

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            return Problem(detail: "Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized");
        }

        if (!user.EmailConfirmed)
            return Problem(detail: "Confirm your email before signing in.", statusCode: StatusCodes.Status403Forbidden, title: "Email not confirmed");

        await users.ResetAccessFailedCountAsync(user);
        await promoter.PromoteAsync(user);
        return Ok(await tokens.IssueAsync(user, cancellationToken));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var issued = await tokens.RefreshAsync(request.RefreshToken, cancellationToken);
        if (issued is null)
            return Unauthorized(Problem(detail: "The refresh token is no longer valid. Sign in again.", statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized"));
        return Ok(issued);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUserId is null)
            return Unauthorized();
        await tokens.RevokeAsync(CurrentUserId, request.RefreshToken, cancellationToken);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<ActionResult<MessageResponse>> ForgotPassword(ForgotPasswordRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is not null)
        {
            if (!user.EmailConfirmed)
                await SendConfirmationAsync(user);
            else
                await SendResetAsync(user);
        }

        return Accepted(new MessageResponse { Message = "If that account exists, a password email is on its way." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var user = await users.FindByIdAsync(request.UserId);
        if (user is null)
            return Bad("This reset link is not valid.");

        var result = await users.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
            return IdentityProblem(result);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<CurrentUserDto>> Me()
    {
        var user = await users.FindByIdAsync(CurrentUserId ?? "");
        if (user is null)
            return Unauthorized();
        var roles = await users.GetRolesAsync(user);
        return Ok(TokenService.ToCurrentUser(user, roles));
    }

    private async Task SendConfirmationAsync(ApplicationUser user)
    {
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var link = BuildLink("/account/confirm-email", user.Id, token);
        await mail.SendAsync(user.Email!, "Confirm your OBWC account",
            $"Confirm your email for the Omaha Beach Wargaming Club site:\n{link}\n", null, null, CancellationToken.None);
    }

    private async Task SendResetAsync(ApplicationUser user)
    {
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var link = BuildLink("/account/reset-password", user.Id, token);
        await mail.SendAsync(user.Email!, "Reset your OBWC password",
            $"Reset your password for the Omaha Beach Wargaming Club site:\n{link}\n", null, null, CancellationToken.None);
    }

    private string BuildLink(string path, string userId, string token)
    {
        var client = appOptions.Value.ClientBaseUrl.TrimEnd('/');
        return $"{client}{path}?userId={Uri.EscapeDataString(userId)}&token={Uri.EscapeDataString(token)}";
    }
}
