using Microsoft.EntityFrameworkCore.Design;

namespace OBWC.Api.Data;

/// <summary>
/// Used by <c>dotnet ef</c> so migrations can be created without starting the site or a live database.
/// The connection string is a placeholder with no password.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=OBWC_DesignTime;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new ApplicationDbContext(options);
    }
}
