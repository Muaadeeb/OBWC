using System.Net;
using System.Net.Http.Json;
using OBWC.Api.Data.Seed;
using OBWC.Shared;
using OBWC.Shared.Dtos;

namespace OBWC.Tests;

public class SeedTests(ApiFactory factory) : IClassFixture<ApiFactory>, IDisposable
{
    private readonly ApiTest _api = new(factory);

    [Fact]
    public void Club_list_has_43_rule_sets_and_gallery_cap_is_100()
    {
        Assert.Equal(43, ClubContent.RuleSets.Length);
        Assert.Equal(43, ClubContent.RuleSets.Select(ruleSet => ruleSet.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(ClubContent.RuleSets, ruleSet => ruleSet.Name == "Flames of War" && ruleSet.PublishedPlayers.Contains("Bill Kuhfahl", StringComparison.Ordinal));
        Assert.Contains(ClubContent.RuleSets, ruleSet => ruleSet.Name == "Advanced Civilization" && ruleSet.PublishedPlayers.Contains("Mike Guldemond", StringComparison.Ordinal));
        Assert.Equal(100, GalleryLimits.MaxPhotos);

        var settings = File.ReadAllText(Path.Combine(RepoRoot(), "src", "OBWC.Api", "appsettings.json"));
        Assert.Contains("\"MaxGalleryPhotos\": 100", settings, StringComparison.Ordinal);
        Assert.Contains("\"Provider\": \"Sqlite\"", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("Password=", settings, StringComparison.OrdinalIgnoreCase);
        var production = File.ReadAllText(Path.Combine(RepoRoot(), "src", "OBWC.Api", "appsettings.Production.json"));
        Assert.Contains("YOUR_SQL_PASSWORD", production, StringComparison.Ordinal);
        Assert.Contains("SqlServer", production, StringComparison.Ordinal);
    }

    [Fact]
    public void SqlServer_migration_is_checked_in()
    {
        var sqlite = Path.Combine(RepoRoot(), "src", "OBWC.Api", "Data", "Migrations");
        Assert.Contains(Directory.GetFiles(sqlite, "*.cs"), file => file.Contains("InitialSqlite", StringComparison.Ordinal) && !file.EndsWith("Designer.cs", StringComparison.Ordinal));

        var sqlServer = Path.Combine(RepoRoot(), "src", "OBWC.Api", "Data", "SqlServerMigrations");
        Assert.Contains(Directory.GetFiles(sqlServer, "*.cs"), file => file.Contains("IdentitySchemaVersion3", StringComparison.Ordinal));
        var snapshot = File.ReadAllText(Path.Combine(sqlServer, "ApplicationDbContextModelSnapshot.cs"));
        Assert.Contains("nvarchar(450)", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Seed_includes_documented_facts_and_no_events()
    {
        var ruleSets = await _api.Client.GetFromJsonAsync<List<RuleSetDto>>("/api/rulesets");
        Assert.NotNull(ruleSets);
        Assert.Equal(43, ruleSets.Count);
        Assert.Contains(ruleSets, ruleSet => ruleSet.Name == "World in Flames");

        var stores = await _api.Client.GetFromJsonAsync<List<StoreDto>>("/api/stores");
        var rad = Assert.Single(stores!);
        Assert.Equal("Rad Comics and Collectibles", rad.Name);
        Assert.Equal("Ground Zero Comics", rad.FormerName);
        Assert.Equal("794 Fort Crook Rd S, Bellevue, NE 68005", rad.Address);
        Assert.Equal("Gameroom", rad.Bullet1);
        Assert.Equal("RPG rules", rad.Bullet2);
        Assert.Equal("Some gems from the old Star Realm", rad.Bullet3);

        var groups = await _api.Client.GetFromJsonAsync<List<GroupDto>>("/api/groups");
        var nadbag = Assert.Single(groups!);
        Assert.Equal("NADBAG", nadbag.Name);
        Assert.Contains("ASL", nadbag.Description, StringComparison.Ordinal);

        var conventions = await _api.Client.GetFromJsonAsync<List<ConventionDto>>("/api/conventions");
        Assert.Contains(conventions!, convention => convention.Name == "Nuke-Con");
        Assert.Contains(conventions!, convention => convention.Name == "ScroogeCon");
        Assert.DoesNotContain(conventions!, convention => !string.IsNullOrWhiteSpace(convention.LocationText));

        var info = await _api.Client.GetFromJsonAsync<PublicInfoDto>("/api/settings/public");
        Assert.Equal("nmh16055@gmail.com", info!.ContactEmail);
        Assert.Contains("$25.00", info.AdultDues, StringComparison.Ordinal);
        Assert.Contains("$10.00", info.StudentDues, StringComparison.Ordinal);
        Assert.Equal("22 and under", info.StudentDuesNote);
        Assert.Equal("Free", info.FriendDues);
        Assert.Equal("May meeting", info.ElectionMeeting);

        var about = await _api.Client.GetFromJsonAsync<ContentPageDto>("/api/content/about");
        Assert.Contains("promote historical and miniature gaming", about!.Body, StringComparison.Ordinal);
        Assert.Contains("May meeting", about.Body, StringComparison.Ordinal);

        var nuke = await _api.Client.GetFromJsonAsync<ContentPageDto>("/api/content/nuke-con");
        Assert.Contains("Bill Kuhfahl", nuke!.Body, StringComparison.Ordinal);

        var officers = await _api.Client.GetFromJsonAsync<List<OfficerDto>>("/api/officers");
        Assert.Empty(officers!);
        var venues = await _api.Client.GetFromJsonAsync<List<VenueDto>>("/api/venues");
        Assert.Empty(venues!);
        var gallery = await _api.Client.GetFromJsonAsync<GalleryListDto>("/api/gallery");
        Assert.Equal(0, gallery!.Count);
        Assert.Equal(2, gallery.MaxPhotos);

        var events = await _api.Client.GetFromJsonAsync<List<CalendarEventDto>>("/api/events?year=2030&month=5");
        Assert.Empty(events!);
        var next = await _api.Client.GetAsync("/api/events/next");
        Assert.Equal(HttpStatusCode.NotFound, next.StatusCode);
    }

    public void Dispose() => _api.Dispose();

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
}
