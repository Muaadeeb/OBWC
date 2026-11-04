namespace OBWC.Api.Authorization;

public class OwnedResourceRequirement : IAuthorizationRequirement;

public class OwnedResourceHandler : AuthorizationHandler<OwnedResourceRequirement, IOwnedResource>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OwnedResourceRequirement requirement,
        IOwnedResource resource)
    {
        if (context.User.IsInRole(RoleNames.Admin) || context.User.IsInRole(RoleNames.SuperAdmin))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");
        if (!string.IsNullOrEmpty(userId)
            && !string.IsNullOrEmpty(resource.OwnerUserId)
            && string.Equals(userId, resource.OwnerUserId, StringComparison.Ordinal))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
