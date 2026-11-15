namespace OBWC.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController(UserManager<ApplicationUser> users, ApplicationDbContext db) : ClubApiController
{
    [HttpGet]
    [Authorize(Policy = PolicyNames.Admin)]
    public async Task<ActionResult<IReadOnlyList<UserSummaryDto>>> List(CancellationToken cancellationToken)
    {
        var accounts = await users.Users.OrderBy(user => user.Email).ToListAsync(cancellationToken);
        var result = new List<UserSummaryDto>(accounts.Count);
        foreach (var account in accounts)
            result.Add(await MapAsync(account));
        return Ok(result);
    }

    [HttpPut("me")]
    [Authorize(Policy = PolicyNames.Member)]
    public async Task<ActionResult<CurrentUserDto>> UpdateProfile(UpdateProfileRequest request)
    {
        var user = await users.FindByIdAsync(CurrentUserId ?? "");
        if (user is null)
            return Unauthorized();

        var displayName = request.DisplayName.Trim();
        if (displayName.Length == 0)
            return Bad("Enter a display name.");

        user.DisplayName = displayName;
        user.MembershipType = request.MembershipType;
        var updated = await users.UpdateAsync(user);
        if (!updated.Succeeded)
            return IdentityProblem(updated);

        var roles = await users.GetRolesAsync(user);
        return Ok(TokenService.ToCurrentUser(user, roles));
    }

    [HttpPut("me/password")]
    [Authorize(Policy = PolicyNames.Member)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await users.FindByIdAsync(CurrentUserId ?? "");
        if (user is null)
            return Unauthorized();

        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return IdentityProblem(result);
        return NoContent();
    }

    [HttpPut("{id}/roles")]
    [Authorize(Policy = PolicyNames.Admin)]
    public async Task<ActionResult<UserSummaryDto>> UpdateRoles(string id, UpdateRolesRequest request)
    {
        if (request.SuperAdmin)
            return Bad("The SuperAdmin role is assigned only from server configuration (Authorization:SuperAdminEmail). It cannot be granted here.");

        var user = await users.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        var targetIsSuperAdmin = await users.IsInRoleAsync(user, RoleNames.SuperAdmin);
        if (targetIsSuperAdmin && !User.IsInRole(RoleNames.SuperAdmin))
            return Forbidden("Only a SuperAdmin can change roles on the configured SuperAdmin account.");

        if (request.Gm && !request.Member && !request.Admin && !targetIsSuperAdmin)
            return Bad("GM is an add-on role for a Member, Admin, or SuperAdmin.");

        var memberResult = await SetRoleAsync(user, RoleNames.Member, request.Member);
        if (memberResult is not null)
            return memberResult;
        var adminResult = await SetRoleAsync(user, RoleNames.Admin, request.Admin);
        if (adminResult is not null)
            return adminResult;
        var gmResult = await SetRoleAsync(user, RoleNames.Gm, request.Gm);
        if (gmResult is not null)
            return gmResult;

        return Ok(await MapAsync(user));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = PolicyNames.Admin)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        if (string.Equals(id, CurrentUserId, StringComparison.Ordinal))
            return Bad("You cannot delete your own account.");

        var user = await users.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        if (await users.IsInRoleAsync(user, RoleNames.SuperAdmin))
            return Forbidden("The configured SuperAdmin account cannot be deleted here.");

        if (await db.PlaySessions.AnyAsync(session => session.CreatedByUserId == id, cancellationToken)
            || await db.GalleryPhotos.AnyAsync(photo => photo.OwnerUserId == id, cancellationToken)
            || await db.EventPhotos.AnyAsync(photo => photo.OwnerUserId == id, cancellationToken)
            || await db.ClubDocuments.AnyAsync(document => document.OwnerUserId == id, cancellationToken))
        {
            return ConflictProblem("This account still owns events, photos, or documents. Remove that content before deleting the account.");
        }

        var attendances = await db.SessionAttendances.Where(row => row.UserId == id).ToListAsync(cancellationToken);
        db.SessionAttendances.RemoveRange(attendances);
        var players = await db.RuleSetPlayers.Where(row => row.OwnerUserId == id).ToListAsync(cancellationToken);
        db.RuleSetPlayers.RemoveRange(players);
        await db.SaveChangesAsync(cancellationToken);

        var deleted = await users.DeleteAsync(user);
        if (!deleted.Succeeded)
            return IdentityProblem(deleted);
        return NoContent();
    }

    private async Task<ActionResult?> SetRoleAsync(ApplicationUser user, string role, bool shouldHave)
    {
        var has = await users.IsInRoleAsync(user, role);
        if (shouldHave == has)
            return null;
        var result = shouldHave
            ? await users.AddToRoleAsync(user, role)
            : await users.RemoveFromRoleAsync(user, role);
        return result.Succeeded ? null : IdentityProblem(result);
    }

    private async Task<UserSummaryDto> MapAsync(ApplicationUser user)
    {
        var roles = await users.GetRolesAsync(user);
        return new UserSummaryDto
        {
            Id = user.Id,
            Email = user.Email ?? "",
            DisplayName = user.DisplayName,
            MembershipType = user.MembershipType,
            EmailConfirmed = user.EmailConfirmed,
            Roles = roles.OrderBy(role => role, StringComparer.Ordinal).ToArray()
        };
    }
}
