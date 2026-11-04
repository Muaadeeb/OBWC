using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OBWC.Api.Data;

namespace OBWC.Tests;

public class SqliteMigrationTests
{
    [Fact]
    public async Task Fresh_sqlite_file_migrates_without_manual_steps()
    {
        var path = Path.Combine(Path.GetTempPath(), $"obwc-migrate-{Guid.NewGuid():N}.db");
        var services = new ServiceCollection();
        services.AddEntityFrameworkSqlite();
        services.Configure<IdentityOptions>(options => options.Stores.SchemaVersion = IdentitySchemaVersions.Version3);
        var provider = services.BuildServiceProvider();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={path};Pooling=False")
            .UseApplicationServiceProvider(provider)
            .Options;

        try
        {
            await using var db = new ApplicationDbContext(options);
            await db.Database.MigrateAsync();

            await using var connection = db.Database.GetDbConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'AspNetUserPasskeys'";
            var table = await command.ExecuteScalarAsync();
            Assert.Equal("AspNetUserPasskeys", table);

            command.CommandText = "SELECT COUNT(*) FROM \"__EFMigrationsHistory\"";
            var migrations = Convert.ToInt32(await command.ExecuteScalarAsync());
            Assert.True(migrations >= 1);
        }
        finally
        {
            TryDelete(path);
            TryDelete(path + "-wal");
            TryDelete(path + "-shm");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
    }
}
