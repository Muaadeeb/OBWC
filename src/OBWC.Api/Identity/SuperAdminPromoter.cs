namespace OBWC.Api.Identity;

public sealed class SuperAdminPromoter(
    UserManager<ApplicationUser> users,
    IConfiguration configuration,
    ILogger<SuperAdminPromoter> logger)
{
    public async Task PromoteConfiguredAccountAsync()
    {
        var email = configuration["Authorization:SuperAdminEmail"]?.Trim();
        if (string.IsNullOrEmpty(email))
            return;

        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            logger.LogInformation("SuperAdmin email is configured, but that account has not registered yet.");
            return;
        }

        await PromoteAsync(user);
    }

    public async Task PromoteAsync(ApplicationUser user)
    {
        var email = configuration["Authorization:SuperAdminEmail"]?.Trim();
        if (string.IsNullOrEmpty(email) || !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
            return;

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
