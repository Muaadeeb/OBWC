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

    /// <summary>
    /// Development uses the same SQL Server database as production. The connection string
    /// stays in the gitignored production file and is not copied into a committed setting.
    /// </summary>
    public static void UseProductionSqlServerForDevelopment(IConfigurationBuilder configuration, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
            return;

        var path = Path.Combine(environment.ContentRootPath, "appsettings.Production.json");
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                "Local development uses the production SQL Server database. appsettings.Production.json is missing, so the connection string is not available.");
        }

        var production = new ConfigurationBuilder().AddJsonFile(path, optional: false).Build();
        var connection = production.GetConnectionString("DefaultConnection");
        if (!IsUsableSqlServerConnection(connection))
        {
            throw new InvalidOperationException(
                "Local development uses the production SQL Server database. The connection string in appsettings.Production.json is not a usable SQL Server connection.");
        }

        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = SqlServerProvider,
            ["ConnectionStrings:DefaultConnection"] = connection
        });
    }

    public static string GetProvider(IConfiguration configuration, IHostEnvironment? environment = null)
    {
        // A Production host is SQL Server even when the base file still says SQLite.
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
