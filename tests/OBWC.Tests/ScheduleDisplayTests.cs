using System.Net;
using System.Net.Http.Json;
using OBWC.Shared;
using OBWC.Shared.Dtos;

namespace OBWC.Tests;

public class ScheduleDisplayTests(ApiFactory factory) : IClassFixture<ApiFactory>, IDisposable
{
    private readonly ApiTest _api = new(factory);

    [Fact]
    public async Task One_off_events_are_not_a_series_and_times_stay_utc()
    {
        var owner = await _api.RegisterAndLoginAsync("series-owner@obwc.test", "Series Owner");
        using var client = _api.As(owner);
        var wall = ClubTime.FromClubWallClock(new DateTime(2026, 7, 14), new TimeSpan(18, 0, 0));
        var single = await client.PostAsJsonAsync("/api/events", new CreateSessionRequest
        {
            Title = "One night",
            ScheduledAt = wall,
            EndsAt = wall.AddHours(4),
            Venue = "Club table",
            Weeks = 1
        });
        Assert.Equal(HttpStatusCode.Created, single.StatusCode);
        var created = await single.Content.ReadFromJsonAsync<CreateSeriesResultDto>();
        var one = await client.GetFromJsonAsync<CalendarEventDto>($"/api/events/{created!.FirstSessionId}");
        Assert.Null(one!.SeriesKey);
        Assert.Equal(wall.ToUniversalTime(), one.ScheduledAt.ToUniversalTime());
        Assert.Equal("6:00 PM CT", ClubTime.FormatTime(one.ScheduledAt));

        var series = await client.PostAsJsonAsync("/api/events", new CreateSessionRequest
        {
            Title = "Weekly",
            ScheduledAt = wall.AddDays(7),
            EndsAt = wall.AddDays(7).AddHours(4),
            Venue = "Club table",
            Weeks = 2
        });
        Assert.Equal(HttpStatusCode.Created, series.StatusCode);
        var seriesBody = await series.Content.ReadFromJsonAsync<CreateSeriesResultDto>();
        var first = await client.GetFromJsonAsync<CalendarEventDto>($"/api/events/{seriesBody!.FirstSessionId}");
        Assert.NotNull(first!.SeriesKey);
        var month = await client.GetFromJsonAsync<List<CalendarEventDto>>("/api/events?year=2026&month=7");
        Assert.Equal(2, month!.Count(item => item.SeriesKey == first.SeriesKey));
    }

    [Fact]
    public async Task A_game_can_name_a_rule_set_that_is_not_on_the_club_list()
    {
        var owner = await _api.RegisterAndLoginAsync("ruleset-typer@obwc.test", "Rule Typist");
        using var client = _api.As(owner);
        var when = DateTimeOffset.UtcNow.AddDays(12);
        var listed = await _api.Client.GetFromJsonAsync<List<RuleSetDto>>("/api/rulesets");
        var flames = listed!.Single(ruleSet => ruleSet.Name == "Flames of War");

        var custom = await client.PostAsJsonAsync("/api/events", new CreateSessionRequest
        {
            Title = "Home rules",
            ScheduledAt = when,
            EndsAt = when.AddHours(4),
            Venue = "Club table",
            RuleSetName = "Chain of Command"
        });
        Assert.Equal(HttpStatusCode.Created, custom.StatusCode);
        var customBody = await custom.Content.ReadFromJsonAsync<CreateSeriesResultDto>();
        var customEvent = await client.GetFromJsonAsync<CalendarEventDto>($"/api/events/{customBody!.FirstSessionId}");
        Assert.Equal("Chain of Command", customEvent!.RuleSetName);
        Assert.Null(customEvent.RuleSetId);

        var after = await _api.Client.GetFromJsonAsync<List<RuleSetDto>>("/api/rulesets");
        Assert.Equal(listed!.Count, after!.Count);
        Assert.DoesNotContain(after, ruleSet => ruleSet.Name == "Chain of Command");

        var matched = await client.PostAsJsonAsync("/api/events", new CreateSessionRequest
        {
            Title = "FoW night",
            ScheduledAt = when.AddDays(1),
            EndsAt = when.AddDays(1).AddHours(4),
            Venue = "Club table",
            RuleSetName = "flames of war"
        });
        Assert.Equal(HttpStatusCode.Created, matched.StatusCode);
        var matchedBody = await matched.Content.ReadFromJsonAsync<CreateSeriesResultDto>();
        var matchedEvent = await client.GetFromJsonAsync<CalendarEventDto>($"/api/events/{matchedBody!.FirstSessionId}");
        Assert.Equal(flames.Id, matchedEvent!.RuleSetId);
        Assert.Equal("Flames of War", matchedEvent.RuleSetName);

        var cleared = await client.PutAsJsonAsync($"/api/events/{customEvent.Id}", new UpdateSessionDetailsRequest
        {
            Title = customEvent.Title,
            Venue = customEvent.Venue,
            RuleSetName = "  "
        });
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        var clearedEvent = await cleared.Content.ReadFromJsonAsync<CalendarEventDto>();
        Assert.Null(clearedEvent!.RuleSetName);
        Assert.Null(clearedEvent.RuleSetId);

        var renamed = await client.PutAsJsonAsync($"/api/events/{matchedEvent.Id}", new UpdateSessionDetailsRequest
        {
            Title = matchedEvent.Title,
            Venue = matchedEvent.Venue,
            RuleSetName = "Bolt Action"
        });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var renamedEvent = await renamed.Content.ReadFromJsonAsync<CalendarEventDto>();
        Assert.Equal("Bolt Action", renamedEvent!.RuleSetName);
        Assert.Null(renamedEvent.RuleSetId);
    }

    [Fact]
    public async Task A_game_can_name_the_person_running_it()
    {
        var owner = await _api.RegisterAndLoginAsync("host-typer@obwc.test", "Schedule Keeper");
        using var client = _api.As(owner);
        // Day 2 of a future month keeps both weeks inside one club-local month.
        var when = new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 2, 18, 0, 0, TimeSpan.Zero);
        if (when <= DateTimeOffset.UtcNow.AddDays(1))
            when = when.AddMonths(1);

        var created = await client.PostAsJsonAsync("/api/events", new CreateSessionRequest
        {
            Title = "Star Wars Armada",
            ScheduledAt = when,
            EndsAt = when.AddHours(4),
            Venue = "Club table",
            HostName = "  Jason Huffstedtler  ",
            Weeks = 2
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<CreateSeriesResultDto>();
        var first = await client.GetFromJsonAsync<CalendarEventDto>($"/api/events/{body!.FirstSessionId}");
        Assert.Equal("Jason Huffstedtler", first!.HostName);
        Assert.Equal("Schedule Keeper", first.OwnerDisplayName);

        var month = await client.GetFromJsonAsync<List<CalendarEventDto>>(
            $"/api/events?year={when.Year}&month={when.Month}");
        var series = month!.Where(item => item.SeriesKey == first.SeriesKey).ToList();
        Assert.Equal(2, series.Count);
        Assert.All(series, item => Assert.Equal("Jason Huffstedtler", item.HostName));

        var renamed = await client.PutAsJsonAsync($"/api/events/{first.Id}", new UpdateSessionDetailsRequest
        {
            Title = first.Title,
            Venue = first.Venue,
            HostName = "Alex Rivera"
        });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var renamedEvent = await renamed.Content.ReadFromJsonAsync<CalendarEventDto>();
        Assert.Equal("Alex Rivera", renamedEvent!.HostName);

        var cleared = await client.PutAsJsonAsync($"/api/events/{first.Id}", new UpdateSessionDetailsRequest
        {
            Title = first.Title,
            Venue = first.Venue,
            HostName = "   "
        });
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        var clearedEvent = await cleared.Content.ReadFromJsonAsync<CalendarEventDto>();
        Assert.Null(clearedEvent!.HostName);
    }

    [Fact]
    public async Task Dev_mail_is_hidden_outside_development_and_confirmation_links_are_captured()
    {
        var hidden = await _api.Client.GetAsync("/api/dev/mail");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        var email = "confirm-link@obwc.test";
        var register = await _api.Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = ApiTest.Password,
            DisplayName = "Confirm Link"
        });
        Assert.Equal(HttpStatusCode.Accepted, register.StatusCode);
        var mail = _api.Outbox.LatestTo(email);
        Assert.NotNull(mail);
        Assert.NotNull(mail.Link);
        Assert.Contains("/account/confirm-email", mail.Link, StringComparison.Ordinal);
        Assert.Contains(mail.Link, mail.Body, StringComparison.Ordinal);
    }

    public void Dispose() => _api.Dispose();
}
