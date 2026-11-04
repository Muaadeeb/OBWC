using OBWC.Api.Data.Seed;
using OBWC.Api.Identity;

namespace OBWC.Api.Data.Seed;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<ApplicationDbContext>();
        var environment = provider.GetRequiredService<IHostEnvironment>();
        var configuration = provider.GetRequiredService<IConfiguration>();

        if (environment.IsEnvironment("Testing"))
            await db.Database.EnsureCreatedAsync(cancellationToken);
        else if (configuration.GetValue("App:ApplyMigrations", true))
            await db.Database.MigrateAsync(cancellationToken);

        await SeedRolesAsync(provider);
        await SeedContentAsync(db, cancellationToken);
        if (environment.IsDevelopment())
            await SampleData.SeedAsync(provider, cancellationToken);
        await provider.GetRequiredService<SuperAdminPromoter>().PromoteConfiguredAccountAsync();
    }

    private static async Task SeedRolesAsync(IServiceProvider provider)
    {
        var roles = provider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in RoleNames.All)
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));
        }
    }

    private static async Task SeedContentAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        if (ClubContent.RuleSets.Length != 43)
            throw new InvalidOperationException($"The club list has 43 rule sets. The seed contains {ClubContent.RuleSets.Length}.");

        if (!await db.RuleSets.AnyAsync(cancellationToken))
        {
            var sort = 1;
            foreach (var ruleSet in ClubContent.RuleSets)
            {
                db.RuleSets.Add(new RuleSet
                {
                    Id = Guid.NewGuid(),
                    Name = ruleSet.Name,
                    PublishedPlayers = ruleSet.PublishedPlayers,
                    SortOrder = sort++
                });
            }
        }

        if (!await db.GameStores.AnyAsync(cancellationToken))
        {
            db.GameStores.Add(new GameStore
            {
                Id = Guid.NewGuid(),
                Name = "Rad Comics and Collectibles",
                FormerName = "Ground Zero Comics",
                Address = "794 Fort Crook Rd S, Bellevue, NE 68005",
                Bullet1 = "Gameroom",
                Bullet2 = "RPG rules",
                Bullet3 = "Some gems from the old Star Realm",
                SortOrder = 1
            });
        }

        if (!await db.GamingGroups.AnyAsync(cancellationToken))
        {
            db.GamingGroups.Add(new GamingGroup
            {
                Id = Guid.NewGuid(),
                Name = "NADBAG",
                Description = "An ASL group.",
                SortOrder = 1
            });
        }

        if (!await db.Conventions.AnyAsync(cancellationToken))
        {
            db.Conventions.AddRange(
                new Convention
                {
                    Id = Guid.NewGuid(),
                    Name = "Nuke-Con",
                    Description = "Bill Kuhfahl is the club's Nuke-Con liaison.",
                    SortOrder = 1
                },
                new Convention
                {
                    Id = Guid.NewGuid(),
                    Name = "ScroogeCon",
                    SortOrder = 2
                });
        }

        if (!await db.ContentPages.AnyAsync(cancellationToken))
        {
            var now = DateTime.UtcNow;
            db.ContentPages.AddRange(
                new ContentPage
                {
                    Key = ContentKeys.About,
                    Title = ClubContent.AboutTitle,
                    Body = ClubContent.AboutBody,
                    UpdatedAtUtc = now
                },
                new ContentPage
                {
                    Key = ContentKeys.NukeCon,
                    Title = ClubContent.NukeConTitle,
                    Body = ClubContent.NukeConBody,
                    UpdatedAtUtc = now
                });
        }

        var existingSettings = await db.SiteSettings.Select(setting => setting.Key).ToListAsync(cancellationToken);
        foreach (var pair in ClubContent.Settings)
        {
            if (existingSettings.Contains(pair.Key))
                continue;
            db.SiteSettings.Add(new SiteSetting { Key = pair.Key, Value = pair.Value });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
