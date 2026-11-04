using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace OBWC.Api.Data;

/// <summary>
/// Used by <c>dotnet ef</c> so migrations can be created without starting the site or a live database.
/// The connection string is a placeholder with no password.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // Identity reads Stores.SchemaVersion from the application service provider.
        // Without this, design-time scaffolding uses schema version 1 while the API
        // runs version 3, and MigrateAsync throws PendingModelChangesWarning.
        var services = new ServiceCollection();
        services.AddEntityFrameworkSqlServer();
        services.Configure<IdentityOptions>(options =>
            options.Stores.SchemaVersion = IdentitySchemaVersions.Version3);
        var provider = services.BuildServiceProvider();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=OBWC_DesignTime;Trusted_Connection=True;TrustServerCertificate=True")
            .UseApplicationServiceProvider(provider)
            .Options;
        return new ApplicationDbContext(options);
    }
}
