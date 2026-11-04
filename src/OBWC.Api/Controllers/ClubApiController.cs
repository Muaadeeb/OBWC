namespace OBWC.Api.Controllers;

public abstract class ClubApiController : ControllerBase
{
    protected string? CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");

    protected bool IsElevated =>
        User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.SuperAdmin);

    protected bool CanSeeMemberDetails =>
        IsElevated || User.IsInRole(RoleNames.Member);

    protected ActionResult Forbidden(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");

    protected ActionResult Bad(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status400BadRequest, title: "Bad request");

    protected ActionResult ConflictProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status409Conflict, title: "Conflict");

    protected ActionResult IdentityProblem(IdentityResult result) =>
        Bad(string.Join(' ', result.Errors.Select(error => error.Description)));

    protected async Task<ActionResult?> DenyUnlessOwnerOrAdminAsync(IAuthorizationService authorization, IOwnedResource resource)
    {
        var result = await authorization.AuthorizeAsync(User, resource, PolicyNames.OwnerOrAdmin);
        if (result.Succeeded)
            return null;
        return Forbidden("You can only change records you own. Admins can change any of them.");
    }

    protected static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
