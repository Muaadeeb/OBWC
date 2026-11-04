using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OBWC.Api.Data;

/// <summary>
/// Used by <c>dotnet ef</c> so migrations can be created without starting the site.
/// The provider follows <c>Database__Provider</c> (Sqlite by default). The connection string has no password.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var providerName = Environment.GetEnvironmentVariable("Database__Provider") ?? DatabaseConfiguration.SqliteProvider;
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connection))
        {
            connection = providerName.Equals(DatabaseConfiguration.SqlServerProvider, StringComparison.OrdinalIgnoreCase)
                ? "Server=localhost;Database=OBWC_DesignTime;Trusted_Connection=True;TrustServerCertificate=True"
                : "Data Source=obwc-design.db";
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = providerName,
                ["ConnectionStrings:DefaultConnection"] = connection
            })
            .Build();

        // Identity reads Stores.SchemaVersion from the application service provider.
        // Without this, design-time scaffolding uses schema version 1 while the API
        // runs version 3, and MigrateAsync throws PendingModelChangesWarning.
        var services = new ServiceCollection();
        if (DatabaseConfiguration.GetProvider(configuration) == DatabaseConfiguration.SqlServerProvider)
            services.AddEntityFrameworkSqlServer();
        else
            services.AddEntityFrameworkSqlite();
        services.Configure<IdentityOptions>(options =>
            options.Stores.SchemaVersion = IdentitySchemaVersions.Version3);
        var provider = services.BuildServiceProvider();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        DatabaseConfiguration.Configure(options, configuration, Directory.GetCurrentDirectory());
        options.UseApplicationServiceProvider(provider);
        return new ApplicationDbContext(options.Options);
    }
}
