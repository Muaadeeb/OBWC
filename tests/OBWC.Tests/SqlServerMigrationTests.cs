using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using OBWC.Api.Data;
using OBWC.Shared;

namespace OBWC.Tests;

public class SqlServerMigrationTests
{
    [Fact]
    public void Production_uses_sql_server_even_when_the_base_settings_say_sqlite()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = DatabaseConfiguration.SqliteProvider,
                ["ConnectionStrings:DefaultConnection"] = "Data Source=App_Data/obwc.db"
            })
            .Build();

        Assert.Equal(DatabaseConfiguration.SqliteProvider, DatabaseConfiguration.GetProvider(configuration));
        Assert.Equal(
            DatabaseConfiguration.SqlServerProvider,
            DatabaseConfiguration.GetProvider(configuration, new StubEnvironment("Production")));
        Assert.False(DatabaseConfiguration.IsUsableSqlServerConnection("Data Source=App_Data/obwc.db"));
        Assert.False(DatabaseConfiguration.IsUsableSqlServerConnection(
            "Data Source=YOUR_SQL_HOST;Initial Catalog=YOUR_SQL_DATABASE;User Id=YOUR_SQL_USER;Password=YOUR_SQL_PASSWORD"));
        Assert.True(DatabaseConfiguration.IsUsableSqlServerConnection(
            "Data Source=SQL5045.site4now.net;Initial Catalog=DB_EXAMPLE_obwc;User Id=DB_EXAMPLE_obwc_admin;Password=not-a-real-password;TrustServerCertificate=True"));
    }

    [Fact]
    public void Sql_server_migrations_match_the_current_model_and_are_not_the_sqlite_set()
    {
        using var db = CreateSqlServerContext();
        var migrations = db.Database.GetMigrations().ToList();
        Assert.Contains(migrations, id => id.Contains("InitialCreate", StringComparison.Ordinal));
        Assert.Contains(migrations, id => id.Contains("IdentitySchemaVersion3", StringComparison.Ordinal));
        Assert.Contains(migrations, id => id.Contains("ClubContentMaintenance", StringComparison.Ordinal));
        Assert.DoesNotContain(migrations, id => id.Contains("InitialSqlite", StringComparison.Ordinal));

        var assembly = typeof(ApplicationDbContext).Assembly;
        var identityFix = assembly.GetTypes().Single(type => type.Name == "IdentitySchemaVersion3");
        Assert.Equal(DatabaseConfiguration.SqlServerMigrationsNamespace, identityFix.Namespace);

        var differ = db.GetService<IMigrationsModelDiffer>();
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot;
        Assert.NotNull(snapshot);
        var source = db.GetService<IModelRuntimeInitializer>().Initialize(snapshot.Model, designTime: true);
        var target = db.GetService<IDesignTimeModel>().Model;
        var differences = differ.GetDifferences(source.GetRelationalModel(), target.GetRelationalModel());
        Assert.True(
            differences.Count == 0,
            "SQL Server migrations are behind the model: " + string.Join("; ", differences.Select(difference => difference.GetType().Name)));
    }

    [Fact]
    public void Idempotent_script_covers_the_sql_server_migrations_and_has_no_secrets()
    {
        var path = Path.Combine(RepoRoot(), "deploy", "sql", "obwc-sqlserver.sql");
        var script = File.ReadAllText(path);
        using var db = CreateSqlServerContext();
        foreach (var id in db.Database.GetMigrations())
            Assert.Contains(id, script, StringComparison.Ordinal);

        Assert.Contains("IF NOT EXISTS", script, StringComparison.Ordinal);
        Assert.Contains("SET QUOTED_IDENTIFIER ON", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [AspNetRoles]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [PlaySessions]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [EventPhotos]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [AspNetUserPasskeys]", script, StringComparison.Ordinal);
        Assert.DoesNotContain("InitialSqlite", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Password=", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("YOUR_SQL", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Seeded_roles_are_member_admin_superadmin_and_gm()
    {
        Assert.Contains(RoleNames.Member, RoleNames.All);
        Assert.Contains(RoleNames.Admin, RoleNames.All);
        Assert.Contains(RoleNames.SuperAdmin, RoleNames.All);
        Assert.Contains(RoleNames.Gm, RoleNames.All);
        Assert.DoesNotContain(RoleNames.All, role => role.Equals("Guest", StringComparison.OrdinalIgnoreCase));
    }

    private static ApplicationDbContext CreateSqlServerContext()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkSqlServer();
        services.Configure<IdentityOptions>(options => options.Stores.SchemaVersion = IdentitySchemaVersions.Version3);
        var provider = services.BuildServiceProvider();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = DatabaseConfiguration.SqlServerProvider,
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=localhost;Database=OBWC_ModelCheck;User Id=unused;Password=unused;TrustServerCertificate=True"
            })
            .Build();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        DatabaseConfiguration.Configure(options, configuration, Path.GetTempPath());
        options.UseApplicationServiceProvider(provider);
        return new ApplicationDbContext(options.Options);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OBWC.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root.");
    }

    private sealed class StubEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "OBWC.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
