using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using OBWC.Api.Scheduling;
using OBWC.Shared;
using OBWC.Shared.Dtos;

namespace OBWC.Tests;

/// <summary>
/// Behavior carried over from the battletechomaha.com campaign calendar:
/// waitlist, check-in window, cancel, series reschedule, and the iCal feed.
/// </summary>
public class CalendarBehaviorTests(ApiFactory factory) : IClassFixture<ApiFactory>, IDisposable
{
    private readonly ApiTest _api = new(factory);

    [Fact]
    public async Task Going_beyond_cap_becomes_waitlist_and_leaving_promotes()
    {
        var (owner, players) = await PeopleAsync();
        var id = await CreateAsync(owner, cap: 2);

        using var a = _api.As(players[0]);
        using var b = _api.As(players[1]);
        using var c = _api.As(players[2]);
        var first = await a.PutAsJsonAsync($"/api/events/{id}/rsvp", new SetRsvpRequest { Status = SessionRsvpStatus.Going });
        var second = await b.PutAsJsonAsync($"/api/events/{id}/rsvp", new SetRsvpRequest { Status = SessionRsvpStatus.Going });
        var third = await c.PutAsJsonAsync($"/api/events/{id}/rsvp", new SetRsvpRequest { Status = SessionRsvpStatus.Going });
        Assert.Equal(SessionRsvpStatus.Going, (await first.Content.ReadFromJsonAsync<RsvpResultDto>())!.Status);
        Assert.Equal(SessionRsvpStatus.Going, (await second.Content.ReadFromJsonAsync<RsvpResultDto>())!.Status);
        var waitlisted = await third.Content.ReadFromJsonAsync<RsvpResultDto>();
        Assert.Equal(SessionRsvpStatus.Waitlist, waitlisted!.Status);
        Assert.Contains("waitlist", waitlisted.Note, StringComparison.OrdinalIgnoreCase);

        await a.PutAsJsonAsync($"/api/events/{id}/rsvp", new SetRsvpRequest { Status = SessionRsvpStatus.NotGoing });
        using var ownerClient = _api.As(owner);
        var roster = await ownerClient.GetFromJsonAsync<List<RosterRowDto>>($"/api/events/{id}/roster");
        Assert.Equal(SessionRsvpStatus.Going, roster!.Single(row => row.UserId == players[2].User.Id).Rsvp);
        Assert.Contains("Waitlist", _api.Factory.Services.GetRequiredService<OBWC.Api.Email.EmailOutbox>().LatestTo(players[2].User.Email)!.Subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Check_in_window_and_owner_bypass()
    {
        var (owner, players) = await PeopleAsync();
        var near = await CreateAsync(owner, start: DateTimeOffset.UtcNow.AddMinutes(30));
        using var player = _api.As(players[0]);
        var self = await player.PostAsJsonAsync($"/api/events/{near}/check-in", new CheckInRequest());
        Assert.Equal(HttpStatusCode.NoContent, self.StatusCode);
        using var ownerClient = _api.As(owner);
        var roster = await ownerClient.GetFromJsonAsync<List<RosterRowDto>>($"/api/events/{near}/roster");
        Assert.Equal(AttendanceStatus.Present, roster!.Single(row => row.UserId == players[0].User.Id).NightStatus);

        var undo = await player.PostAsJsonAsync($"/api/events/{near}/check-in/undo", new CheckInRequest());
        Assert.Equal(HttpStatusCode.NoContent, undo.StatusCode);

        var far = await CreateAsync(owner, start: DateTimeOffset.UtcNow.AddDays(3));
        var tooEarly = await player.PostAsJsonAsync($"/api/events/{far}/check-in", new CheckInRequest());
        Assert.Equal(HttpStatusCode.BadRequest, tooEarly.StatusCode);
        Assert.Contains("near event", await tooEarly.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var ownerChecks = await ownerClient.PostAsJsonAsync($"/api/events/{far}/check-in", new CheckInRequest { UserId = players[0].User.Id });
        Assert.Equal(HttpStatusCode.NoContent, ownerChecks.StatusCode);

        await _api.RegisterAndLoginAsync("other-gm@obwc.test", "Other GM");
        await _api.AddRolesAsync("other-gm@obwc.test", RoleNames.Gm);
        var otherGm = await LoginAsync("other-gm@obwc.test");
        using var gmClient = _api.As(otherGm);
        var gmChecks = await gmClient.PostAsJsonAsync($"/api/events/{far}/check-in", new CheckInRequest { UserId = players[1].User.Id });
        Assert.Equal(HttpStatusCode.Forbidden, gmChecks.StatusCode);
    }

    [Fact]
    public async Task Cancel_stores_reason_blocks_rsvp_and_emails()
    {
        var (owner, players) = await PeopleAsync();
        var id = await CreateAsync(owner);
        using var player = _api.As(players[0]);
        await player.PutAsJsonAsync($"/api/events/{id}/rsvp", new SetRsvpRequest { Status = SessionRsvpStatus.Going });
        using var ownerClient = _api.As(owner);
        var cancel = await ownerClient.PostAsJsonAsync($"/api/events/{id}/cancel", new CancelSessionRequest
        {
            Reason = "Store closed",
            Notify = true
        });
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        var blocked = await player.PutAsJsonAsync($"/api/events/{id}/rsvp", new SetRsvpRequest { Status = SessionRsvpStatus.Maybe });
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Contains("cancelled", await blocked.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        var view = await _api.Client.GetFromJsonAsync<CalendarEventDto>($"/api/events/{id}");
        Assert.True(view!.IsCancelled);
        Assert.Equal("Store closed", view.CancellationReason);
        Assert.Contains("Cancelled", _api.Factory.Services.GetRequiredService<OBWC.Api.Email.EmailOutbox>().LatestTo(players[0].User.Email)!.Subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reschedule_series_shifts_following_weeks_and_cap_demotes_going()
    {
        var (owner, players) = await PeopleAsync();
        var start = new DateTimeOffset(2032, 6, 5, 18, 0, 0, TimeSpan.FromHours(-5));
        using var ownerClient = _api.As(owner);
        var created = await ownerClient.PostAsJsonAsync("/api/events", new CreateSessionRequest
        {
            Title = "Weekly night",
            ScheduledAt = start,
            EndsAt = start.AddHours(4),
            Venue = "Old Hall",
            PlayerCap = 3,
            Weeks = 2,
            IsPublished = true,
            AllowPlayerRsvp = true
        });
        var series = await created.Content.ReadFromJsonAsync<CreateSeriesResultDto>();
        var first = await ownerClient.GetFromJsonAsync<CalendarEventDto>($"/api/events/{series!.FirstSessionId}");
        var month = await ownerClient.GetFromJsonAsync<List<CalendarEventDto>>("/api/events?year=2032&month=6");
        var second = month!.Single(item => item.SeriesKey == first!.SeriesKey && item.Id != first.Id);

        foreach (var auth in players)
        {
            using var client = _api.As(auth);
            await client.PutAsJsonAsync($"/api/events/{first!.Id}/rsvp", new SetRsvpRequest { Status = SessionRsvpStatus.Going });
        }

        var tightened = await ownerClient.PutAsJsonAsync($"/api/events/{first!.Id}", new UpdateSessionDetailsRequest
        {
            Title = "Renamed night",
            Venue = "New Shop",
            Address = "9 Maple",
            LocationNotes = "Back room",
            PlayerCap = 1
        });
        Assert.Equal(HttpStatusCode.OK, tightened.StatusCode);
        var updated = await tightened.Content.ReadFromJsonAsync<CalendarEventDto>();
        Assert.Equal("New Shop", updated!.Venue);
        Assert.Equal(1, updated.PlayerCap);
        Assert.Equal(1, updated.Going);
        Assert.Equal(2, updated.Waitlist);

        var newStart = start.AddDays(7);
        var moved = await ownerClient.PostAsJsonAsync($"/api/events/{first.Id}/reschedule", new RescheduleSessionRequest
        {
            NewStart = newStart,
            NewEnd = newStart.AddHours(4),
            EntireSeriesFollowing = true,
            Notify = false
        });
        Assert.Equal(HttpStatusCode.NoContent, moved.StatusCode);
        var movedFirst = await ownerClient.GetFromJsonAsync<CalendarEventDto>($"/api/events/{first.Id}");
        var movedSecond = await ownerClient.GetFromJsonAsync<CalendarEventDto>($"/api/events/{second.Id}");
        Assert.Equal(newStart, movedFirst!.ScheduledAt);
        Assert.Equal(start.AddDays(14), movedSecond!.ScheduledAt);
    }

    [Fact]
    public async Task My_schedule_lists_only_my_rsvp_or_check_in_and_ics_skips_cancelled()
    {
        var (owner, players) = await PeopleAsync();
        var past = await CreateAsync(owner, start: DateTimeOffset.UtcNow.AddDays(-2));
        using var ownerClient = _api.As(owner);
        await ownerClient.PostAsJsonAsync($"/api/events/{past}/check-in", new CheckInRequest { UserId = players[0].User.Id });
        using var other = _api.As(players[1]);
        var mine = await other.GetFromJsonAsync<List<CalendarEventDto>>("/api/events/mine");
        Assert.DoesNotContain(mine!, item => item.Id == past);
        await other.PutAsJsonAsync($"/api/events/{past}/rsvp", new SetRsvpRequest { Status = SessionRsvpStatus.Going });
        mine = await other.GetFromJsonAsync<List<CalendarEventDto>>("/api/events/mine");
        Assert.Contains(mine!, item => item.Id == past);

        var icsResponse = await other.GetAsync("/api/events/calendar.ics");
        Assert.Equal(HttpStatusCode.OK, icsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.Client.GetAsync("/api/events/calendar.ics")).StatusCode);

        var sample = ScheduleService.BuildIcs("Cal Test", "cal-test",
        [
            new CalendarEventDto
            {
                Id = Guid.NewGuid(),
                ScheduledAt = DateTimeOffset.UtcNow.AddDays(1),
                EndsAt = DateTimeOffset.UtcNow.AddDays(1).AddHours(4),
                Title = "Night 1",
                Notes = "Notes",
                IsPublished = true,
                Venue = "Game Shop",
                Address = "1 Table Rd",
                OnlineLink = "https://discord.gg/x",
                LocationNotes = "Door code 12"
            },
            new CalendarEventDto
            {
                Id = Guid.NewGuid(),
                ScheduledAt = DateTimeOffset.UtcNow.AddDays(2),
                Title = "Cancelled night",
                IsPublished = true,
                IsCancelled = true
            }
        ]);
        Assert.Contains("LOCATION:Game Shop\\, 1 Table Rd", sample, StringComparison.Ordinal);
        Assert.Contains("Door code 12", sample, StringComparison.Ordinal);
        Assert.Contains("Night 1", sample, StringComparison.Ordinal);
        Assert.DoesNotContain("Cancelled night", sample, StringComparison.Ordinal);
        Assert.Contains("BEGIN:VCALENDAR", sample, StringComparison.Ordinal);
    }

    private async Task<(AuthResponse Owner, AuthResponse[] Players)> PeopleAsync()
    {
        var owner = await EnsureAsync("cal-owner@obwc.test", "Calendar Owner");
        var players = new[]
        {
            await EnsureAsync("cal-a@obwc.test", "Player A"),
            await EnsureAsync("cal-b@obwc.test", "Player B"),
            await EnsureAsync("cal-c@obwc.test", "Player C")
        };
        return (owner, players);
    }

    private async Task<AuthResponse> EnsureAsync(string email, string name)
    {
        var login = await _api.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = ApiTest.Password });
        if (login.IsSuccessStatusCode)
            return (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        return await _api.RegisterAndLoginAsync(email, name);
    }

    private async Task<Guid> CreateAsync(AuthResponse owner, int? cap = null, DateTimeOffset? start = null)
    {
        var when = start ?? DateTimeOffset.UtcNow.AddDays(10);
        using var client = _api.As(owner);
        var response = await client.PostAsJsonAsync("/api/events", new CreateSessionRequest
        {
            Title = "Store night",
            ScheduledAt = when,
            EndsAt = when.AddHours(4),
            Venue = "Bellevue Game Shop",
            Address = "123 Main St",
            OnlineLink = "https://discord.gg/example",
            LocationNotes = "Park in back",
            Notes = "Bring dice",
            PlayerCap = cap,
            IsPublished = true,
            AllowPlayerRsvp = true,
            Weeks = 1
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<CreateSeriesResultDto>();
        return body!.FirstSessionId!.Value;
    }

    private async Task<AuthResponse> LoginAsync(string email)
    {
        var response = await _api.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = ApiTest.Password });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    public void Dispose() => _api.Dispose();
}
