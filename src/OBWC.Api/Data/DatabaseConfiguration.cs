using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Hosting;

namespace OBWC.Api.Data;

public static class DatabaseConfiguration
{
    public const string SqliteProvider = "Sqlite";
    public const string SqlServerProvider = "SqlServer";
    public const string SqliteMigrationsNamespace = "OBWC.Api.Data.Migrations";
    public const string SqlServerMigrationsNamespace = "OBWC.Api.Data.SqlServerMigrations";

    public static string GetProvider(IConfiguration configuration, IHostEnvironment? environment = null)
    {
        // The live site is SQL Server. appsettings.json keeps SQLite so a local
        // Development run still works when the production file is absent.
        if (environment?.IsProduction() == true)
            return SqlServerProvider;

        var value = configuration["Database:Provider"]?.Trim();
        if (string.IsNullOrEmpty(value) || value.Equals(SqliteProvider, StringComparison.OrdinalIgnoreCase))
            return SqliteProvider;
        if (value.Equals(SqlServerProvider, StringComparison.OrdinalIgnoreCase))
            return SqlServerProvider;
        throw new InvalidOperationException(
            $"Database:Provider '{value}' is not supported. Use {SqliteProvider} or {SqlServerProvider}.");
    }

    public static void Configure(
        DbContextOptionsBuilder options,
        IConfiguration configuration,
        string contentRoot,
        IHostEnvironment? environment = null)
    {
        var connection = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        if (GetProvider(configuration, environment) == SqlServerProvider)
            options.UseSqlServer(connection, sql => sql.EnableRetryOnFailure());
        else
            options.UseSqlite(ResolveSqliteConnection(connection, contentRoot));

        // Both provider migration sets live in this assembly. EF must see only the active one.
        options.ReplaceService<IMigrationsAssembly, ProviderMigrationsAssembly>();
    }

    public static bool IsUsableSqlServerConnection(string? connection)
    {
        if (string.IsNullOrWhiteSpace(connection))
            return false;
        if (connection.Contains("YOUR_SQL", StringComparison.OrdinalIgnoreCase)
            || connection.Contains("SET_WITH", StringComparison.OrdinalIgnoreCase)
            || connection.Contains(":memory:", StringComparison.OrdinalIgnoreCase))
            return false;

        var hasDatabase = ContainsKey(connection, "Database") || ContainsKey(connection, "Initial Catalog");
        var hasServer = ContainsKey(connection, "Server")
            || ContainsKey(connection, "Data Source")
            || ContainsKey(connection, "Addr")
            || ContainsKey(connection, "Address")
            || ContainsKey(connection, "Network Address");
        // SQLite is "Data Source=App_Data/obwc.db" and has no catalog.
        // SmarterASP strings also use Data Source=, and they name Initial Catalog.
        return hasDatabase && hasServer;
    }

    public static string ResolveSqliteConnection(string connection, string contentRoot)
    {
        var builder = new SqliteConnectionStringBuilder(connection);
        if (!string.Equals(builder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase)
            && !Path.IsPathRooted(builder.DataSource))
        {
            var full = Path.GetFullPath(Path.Combine(contentRoot, builder.DataSource));
            var directory = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            builder.DataSource = full;
        }

        return builder.ToString();
    }

    private static bool ContainsKey(string connection, string key)
        => connection.Contains(key + "=", StringComparison.OrdinalIgnoreCase);
}
