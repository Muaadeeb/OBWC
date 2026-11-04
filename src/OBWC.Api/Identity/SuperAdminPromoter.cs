namespace OBWC.Api.Identity;

public sealed class SuperAdminPromoter(
    UserManager<ApplicationUser> users,
    IConfiguration configuration,
    ILogger<SuperAdminPromoter> logger)
{
    public Task PromoteConfiguredAccountAsync() => SyncAsync();

    public Task PromoteAsync(ApplicationUser user) => SyncAsync();

    /// <summary>
    /// SuperAdmin is the account named by Authorization:SuperAdminEmail, and no other account.
    /// </summary>
    public async Task SyncAsync()
    {
        var email = configuration["Authorization:SuperAdminEmail"]?.Trim();
        var holders = await users.GetUsersInRoleAsync(RoleNames.SuperAdmin);
        foreach (var holder in holders)
        {
            if (!string.IsNullOrEmpty(email) && string.Equals(holder.Email, email, StringComparison.OrdinalIgnoreCase))
                continue;
            var removed = await users.RemoveFromRoleAsync(holder, RoleNames.SuperAdmin);
            if (!removed.Succeeded)
                logger.LogError("Could not remove SuperAdmin from {Email}: {Errors}", holder.Email, string.Join(' ', removed.Errors.Select(error => error.Description)));
            else
                logger.LogInformation("Removed SuperAdmin from {Email} because it is not Authorization:SuperAdminEmail.", holder.Email);
        }

        if (string.IsNullOrEmpty(email))
            return;

        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            logger.LogInformation("SuperAdmin email is configured, but that account has not registered yet.");
            return;
        }

        if (!await users.IsInRoleAsync(user, RoleNames.SuperAdmin))
        {
            var result = await users.AddToRoleAsync(user, RoleNames.SuperAdmin);
            if (!result.Succeeded)
                logger.LogError("Could not assign SuperAdmin: {Errors}", string.Join(' ', result.Errors.Select(error => error.Description)));
            else
                logger.LogInformation("Assigned SuperAdmin from server configuration.");
        }

        if (!await users.IsInRoleAsync(user, RoleNames.Member))
            await users.AddToRoleAsync(user, RoleNames.Member);
    }
}
