namespace OBWC.Api.Data.Seed;

/// <summary>
/// Development-only rows so the schedule and sign-in can be tried on a fresh clone.
/// Every title and name starts with SAMPLE. Nothing here is a club fact.
/// </summary>
public static class SampleData
{
    public const string MemberEmail = "sample.member@obwc.local";
    public const string AdminEmail = "sample.admin@obwc.local";
    public const string MemberName = "SAMPLE Member";
    public const string AdminName = "SAMPLE Admin";

    public static async Task SeedAsync(IServiceProvider provider, CancellationToken cancellationToken)
    {
        var configuration = provider.GetRequiredService<IConfiguration>();
        var password = configuration["Seed:SamplePassword"];
        if (string.IsNullOrWhiteSpace(password))
            return;

        var users = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var member = await EnsureUserAsync(users, MemberEmail, MemberName, password, MembershipType.Adult);
        var admin = await EnsureUserAsync(users, AdminEmail, AdminName, password, MembershipType.Adult);
        if (member is null)
            return;

        await EnsureRoleAsync(users, member, RoleNames.Member);
        if (admin is not null)
        {
            await EnsureRoleAsync(users, admin, RoleNames.Member);
            await EnsureRoleAsync(users, admin, RoleNames.Admin);
        }

        var db = provider.GetRequiredService<ApplicationDbContext>();
        if (await db.PlaySessions.AnyAsync(session => session.Title != null && session.Title.StartsWith("SAMPLE"), cancellationToken))
            return;

        var schedule = provider.GetRequiredService<ScheduleService>();
        var nextSaturday = NextWeekday(ClubTime.ClubToday(), DayOfWeek.Saturday);
        var start = ClubTime.FromClubWallClock(nextSaturday, new TimeSpan(18, 0, 0));
        var end = ClubTime.FromClubWallClock(nextSaturday, new TimeSpan(22, 0, 0));
        var (ok, _, error) = await schedule.CreateSessionAsync(
            start,
            end,
            "SAMPLE — upcoming game night",
            "SAMPLE data. Development only. Not a real club event.",
            member.Id,
            venue: "SAMPLE — venue not entered",
            cancellationToken: cancellationToken);
        if (!ok)
            throw new InvalidOperationException(error ?? "Could not seed the sample event.");

        var seriesStart = ClubTime.FromClubWallClock(nextSaturday.AddDays(7), new TimeSpan(13, 0, 0));
        var series = await schedule.CreateWeeklySeriesAsync(new CreateSessionRequest
        {
            Title = "SAMPLE — weekly series",
            Notes = "SAMPLE data. Development only. Not a real club event.",
            ScheduledAt = seriesStart,
            EndsAt = seriesStart.AddHours(4),
            Venue = "SAMPLE — venue not entered",
            Weeks = 2,
            IsPublished = true,
            AllowPlayerRsvp = true
        }, member.Id, cancellationToken);
        if (!series.Ok)
            throw new InvalidOperationException(series.Error ?? "Could not seed the sample series.");

        var sampleIds = await db.PlaySessions
            .Where(session => session.Title != null && session.Title.StartsWith("SAMPLE"))
            .Select(session => session.Id)
            .ToListAsync(cancellationToken);
        foreach (var id in sampleIds)
            await schedule.SetPlayerRsvpAsync(id, member.Id, SessionRsvpStatus.Going, cancellationToken);
    }

    private static async Task<ApplicationUser?> EnsureUserAsync(
        UserManager<ApplicationUser> users,
        string email,
        string displayName,
        string password,
        MembershipType membership)
    {
        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
            return existing;

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
            MembershipType = membership
        };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
            throw new InvalidOperationException(string.Join(' ', created.Errors.Select(error => error.Description)));
        return user;
    }

    private static async Task EnsureRoleAsync(UserManager<ApplicationUser> users, ApplicationUser user, string role)
    {
        if (!await users.IsInRoleAsync(user, role))
            await users.AddToRoleAsync(user, role);
    }

    private static DateTime NextWeekday(DateTime from, DayOfWeek day)
    {
        var days = ((int)day - (int)from.DayOfWeek + 7) % 7;
        if (days == 0)
            days = 7;
        return from.Date.AddDays(days);
    }
}
