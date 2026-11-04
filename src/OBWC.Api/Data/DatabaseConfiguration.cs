using Microsoft.Data.Sqlite;

namespace OBWC.Api.Data;

public static class DatabaseConfiguration
{
    public const string SqliteProvider = "Sqlite";
    public const string SqlServerProvider = "SqlServer";

    public static string GetProvider(IConfiguration configuration)
    {
        var value = configuration["Database:Provider"]?.Trim();
        if (string.IsNullOrEmpty(value) || value.Equals(SqliteProvider, StringComparison.OrdinalIgnoreCase))
            return SqliteProvider;
        if (value.Equals(SqlServerProvider, StringComparison.OrdinalIgnoreCase))
            return SqlServerProvider;
        throw new InvalidOperationException(
            $"Database:Provider '{value}' is not supported. Use {SqliteProvider} or {SqlServerProvider}.");
    }

    public static void Configure(DbContextOptionsBuilder options, IConfiguration configuration, string contentRoot)
    {
        var connection = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        if (GetProvider(configuration) == SqlServerProvider)
        {
            options.UseSqlServer(connection, sql => sql.EnableRetryOnFailure());
            return;
        }

        options.UseSqlite(ResolveSqliteConnection(connection, contentRoot));
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
}
