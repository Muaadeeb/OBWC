using System.Net;
using System.Net.Http.Json;
using OBWC.Shared;
using OBWC.Shared.Dtos;

namespace OBWC.Tests;

public class GameDayTests(ApiFactory factory) : IClassFixture<ApiFactory>, IDisposable
{
    private static readonly DateTimeOffset Start = new(2034, 5, 12, 15, 0, 0, TimeSpan.Zero);

    private readonly ApiTest _api = new(factory);

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Non_member_cannot_create_a_game_day()
    {
        var guest = await _api.RegisterAndLoginAsync(Email("day-guest"), "Day Guest", membership: null);
        using var client = _api.As(guest);
        var denied = await client.PostAsJsonAsync("/api/game-days", Day());
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Contains("Only members may schedule a game.", await denied.Content.ReadAsStringAsync());

        var anonymous = await _api.Client.PostAsJsonAsync("/api/game-days", Day());
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task Game_must_stay_inside_the_game_day_and_uses_its_place()
    {
        var host = await _api.RegisterAndLoginAsync(Email("window-host"), "Window Host");
        using var client = _api.As(host);
        var dayId = await CreateDayAsync(client);

        var outside = await client.PostAsJsonAsync("/api/events", Game(dayId, Start.AddHours(-1), Start.AddHours(2)));
        Assert.Equal(HttpStatusCode.BadRequest, outside.StatusCode);
        Assert.Contains("inside the game day", await outside.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var missingEnd = await client.PostAsJsonAsync("/api/events", Game(dayId, Start.AddHours(1), null));
        Assert.Equal(HttpStatusCode.BadRequest, missingEnd.StatusCode);
        Assert.Contains("whole game day", await missingEnd.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var weekly = await client.PostAsJsonAsync("/api/events", Game(dayId, Start, Start.AddHours(8), weeks: 2, follow: true));
        Assert.Equal(HttpStatusCode.BadRequest, weekly.StatusCode);
        Assert.Contains("one game day at a time", await weekly.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var added = await client.PostAsJsonAsync("/api/events", Game(dayId, Start, Start.AddHours(8), follow: true, title: "Morning Spearhead"));
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var gameId = (await added.Content.ReadFromJsonAsync<CreateSeriesResultDto>())!.FirstSessionId!.Value;
        var game = await client.GetFromJsonAsync<CalendarEventDto>($"/api/events/{gameId}");
        Assert.Equal(dayId, game!.GameDayId);
        Assert.Equal(Start, game.ScheduledAt);
        Assert.Equal(Start.AddHours(8), game.EndsAt);
        Assert.Equal("Front Hall", game.Venue);
        Assert.Equal("100 Beach St", game.Address);
        Assert.Equal("Game", game.ScheduleKind);

        var table = await client.PostAsJsonAsync("/api/events", Game(dayId, Start.AddHours(2), Start.AddHours(5), title: "Afternoon Bolt Action"));
        Assert.Equal(HttpStatusCode.Created, table.StatusCode);

        var pastEnd = await client.PostAsJsonAsync("/api/events", Game(dayId, Start.AddHours(6), Start.AddHours(9), title: "Too late"));
        Assert.Equal(HttpStatusCode.BadRequest, pastEnd.StatusCode);

        var listed = await client.GetFromJsonAsync<GameDayDto>($"/api/game-days/{dayId}");
        Assert.Equal(2, listed!.Games.Count);
        Assert.Equal("Front Hall", listed.Venue);
        Assert.Contains(listed.Games, item => item.Title == "Morning Spearhead");

        var emptyId = await CreateDayAsync(client, "Empty Saturday", Start.AddDays(14));
        var empty = await client.GetFromJsonAsync<GameDayDto>($"/api/game-days/{emptyId}");
        Assert.Empty(empty!.Games);

        var seriesResponse = await client.PostAsJsonAsync("/api/game-days", Day("Weekly hall", weeks: 2));
        Assert.Equal(HttpStatusCode.Created, seriesResponse.StatusCode);
        var month = await client.GetFromJsonAsync<List<GameDayDto>>("/api/game-days?year=2034&month=5");
        var weeklyDays = month!.Where(day => day.Title != null && day.Title.StartsWith("Weekly hall", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, weeklyDays.Count);
        Assert.All(weeklyDays, day => Assert.Empty(day.Games));
        Assert.NotNull(weeklyDays[0].SeriesKey);
    }

    [Fact]
    public async Task Roster_lists_going_maybe_and_waitlist_only()
    {
        var host = await _api.RegisterAndLoginAsync(Email("roster-host"), "Roster Host");
        var going = await _api.RegisterAndLoginAsync(Email("roster-going"), "Going Player");
        var maybe = await _api.RegisterAndLoginAsync(Email("roster-maybe"), "Maybe Player");
        var no = await _api.RegisterAndLoginAsync(Email("roster-no"), "No Player");
        var waiting = await _api.RegisterAndLoginAsync(Email("roster-wait"), "Wait Player");
        var silent = await _api.RegisterAndLoginAsync(Email("roster-silent"), "Silent Player");
        using var hostClient = _api.As(host);
        var dayId = await CreateDayAsync(hostClient, "Roster day");
        var gameId = await CreateGameAsync(hostClient, dayId, cap: 1, title: "Capped table");

        var roster = await hostClient.GetFromJsonAsync<List<RosterRowDto>>($"/api/events/{gameId}/roster");
        Assert.Empty(roster!);

        using var goingClient = _api.As(going);
        using var maybeClient = _api.As(maybe);
        using var noClient = _api.As(no);
        using var waitClient = _api.As(waiting);
        Assert.Equal(HttpStatusCode.OK, (await goingClient.PutAsJsonAsync(Rsvp(gameId), new SetRsvpRequest { Status = SessionRsvpStatus.Going })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await maybeClient.PutAsJsonAsync(Rsvp(gameId), new SetRsvpRequest { Status = SessionRsvpStatus.Maybe })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await noClient.PutAsJsonAsync(Rsvp(gameId), new SetRsvpRequest { Status = SessionRsvpStatus.NotGoing })).StatusCode);
        var waitResult = await waitClient.PutAsJsonAsync(Rsvp(gameId), new SetRsvpRequest { Status = SessionRsvpStatus.Going });
        Assert.Equal(SessionRsvpStatus.Waitlist, (await waitResult.Content.ReadFromJsonAsync<RsvpResultDto>())!.Status);

        roster = await hostClient.GetFromJsonAsync<List<RosterRowDto>>($"/api/events/{gameId}/roster");
        Assert.Contains(roster!, row => row.UserId == going.User.Id && row.Rsvp == SessionRsvpStatus.Going);
        Assert.Contains(roster!, row => row.UserId == maybe.User.Id && row.Rsvp == SessionRsvpStatus.Maybe);
        Assert.Contains(roster!, row => row.UserId == waiting.User.Id && row.Rsvp == SessionRsvpStatus.Waitlist);
        Assert.DoesNotContain(roster!, row => row.UserId == no.User.Id);
        Assert.DoesNotContain(roster!, row => row.UserId == silent.User.Id);

        var noView = await noClient.GetFromJsonAsync<CalendarEventDto>($"/api/events/{gameId}");
        Assert.Equal(SessionRsvpStatus.NotGoing, noView!.MyRsvp);

        var checkedIn = await hostClient.PostAsJsonAsync($"/api/events/{gameId}/check-in", new CheckInRequest { UserId = silent.User.Id });
        Assert.Equal(HttpStatusCode.NoContent, checkedIn.StatusCode);
        roster = await hostClient.GetFromJsonAsync<List<RosterRowDto>>($"/api/events/{gameId}/roster");
        Assert.Contains(roster!, row => row.UserId == silent.User.Id && row.NightStatus == AttendanceStatus.Present);

        var day = await hostClient.GetFromJsonAsync<GameDayDto>($"/api/game-days/{dayId}");
        Assert.Equal(1, day!.PlayersGoing);

        var second = await CreateGameAsync(hostClient, dayId, title: "Second table");
        Assert.Equal(HttpStatusCode.OK, (await goingClient.PutAsJsonAsync(Rsvp(second), new SetRsvpRequest { Status = SessionRsvpStatus.Going })).StatusCode);
        day = await hostClient.GetFromJsonAsync<GameDayDto>($"/api/game-days/{dayId}");
        Assert.Equal(1, day!.PlayersGoing);
    }

    [Fact]
    public async Task Host_detaches_and_only_the_game_owner_edits_or_cancels()
    {
        var host = await _api.RegisterAndLoginAsync(Email("detach-host"), "Detach Host");
        var gm = await _api.RegisterAndLoginAsync(Email("detach-gm"), "Table GM");
        var adminEmail = Email("detach-admin");
        await _api.RegisterAndLoginAsync(adminEmail, "Detach Admin");
        await _api.AddRolesAsync(adminEmail, RoleNames.Admin);
        var admin = await LoginAsync(adminEmail);

        using var hostClient = _api.As(host);
        using var gmClient = _api.As(gm);
        using var adminClient = _api.As(admin);
        var dayId = await CreateDayAsync(hostClient, "Detach day");
        var gameId = await CreateGameAsync(gmClient, dayId, title: "GM table", online: "https://discord.gg/table");

        var hostEdit = await hostClient.PutAsJsonAsync($"/api/events/{gameId}", new UpdateSessionDetailsRequest { Title = "Stolen", Venue = "Elsewhere" });
        Assert.Equal(HttpStatusCode.Forbidden, hostEdit.StatusCode);
        var hostCancel = await hostClient.PostAsJsonAsync($"/api/events/{gameId}/cancel", new CancelSessionRequest { Notify = false });
        Assert.Equal(HttpStatusCode.Forbidden, hostCancel.StatusCode);

        var gmEdit = await gmClient.PutAsJsonAsync($"/api/events/{gameId}", new UpdateSessionDetailsRequest
        {
            Title = "My table",
            Venue = "Should stay off the card"
        });
        Assert.Equal(HttpStatusCode.OK, gmEdit.StatusCode);
        var edited = await gmEdit.Content.ReadFromJsonAsync<CalendarEventDto>();
        Assert.Equal("My table", edited!.Title);
        Assert.Equal("Front Hall", edited.Venue);

        var gmDetach = await gmClient.PostAsync($"/api/events/{gameId}/detach", null);
        Assert.Equal(HttpStatusCode.Forbidden, gmDetach.StatusCode);

        var detach = await hostClient.PostAsync($"/api/events/{gameId}/detach", null);
        Assert.Equal(HttpStatusCode.NoContent, detach.StatusCode);
        var detached = await hostClient.GetFromJsonAsync<CalendarEventDto>($"/api/events/{gameId}");
        Assert.Null(detached!.GameDayId);
        Assert.Equal("Front Hall", detached.Venue);
        Assert.Equal("100 Beach St", detached.Address);
        Assert.Equal("https://discord.gg/table", detached.OnlineLink);

        var another = await CreateGameAsync(gmClient, dayId, title: "Admin takes it off");
        var adminDetach = await adminClient.PostAsync($"/api/events/{another}/detach", null);
        Assert.Equal(HttpStatusCode.NoContent, adminDetach.StatusCode);
        var takenOff = await adminClient.GetFromJsonAsync<CalendarEventDto>($"/api/events/{another}");
        Assert.Null(takenOff!.GameDayId);
        Assert.Equal("Front Hall", takenOff.Venue);
    }

    [Fact]
    public async Task Cancelling_a_game_day_cancels_its_games_and_reschedule_keeps_them_inside()
    {
        var host = await _api.RegisterAndLoginAsync(Email("move-host"), "Move Host");
        using var client = _api.As(host);
        var dayId = await CreateDayAsync(client, "Move day");
        var early = await CreateGameAsync(client, dayId, Start, Start.AddHours(2), "Early");
        var late = await CreateGameAsync(client, dayId, Start.AddHours(6), Start.AddHours(8), "Late");

        var shorten = await client.PostAsJsonAsync($"/api/game-days/{dayId}/reschedule", new RescheduleSessionRequest
        {
            NewStart = Start,
            NewEnd = Start.AddHours(5),
            Notify = false
        });
        Assert.Equal(HttpStatusCode.BadRequest, shorten.StatusCode);
        Assert.Contains("outside the game day", await shorten.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        var still = await client.GetFromJsonAsync<CalendarEventDto>($"/api/events/{late}");
        Assert.Equal(Start.AddHours(8), still!.EndsAt);

        var shift = await client.PostAsJsonAsync($"/api/game-days/{dayId}/reschedule", new RescheduleSessionRequest
        {
            NewStart = Start.AddDays(1),
            NewEnd = Start.AddDays(1).AddHours(8),
            Notify = false
        });
        Assert.Equal(HttpStatusCode.NoContent, shift.StatusCode);
        var moved = await client.GetFromJsonAsync<CalendarEventDto>($"/api/events/{early}");
        Assert.Equal(Start.AddDays(1), moved!.ScheduledAt);
        Assert.Equal(dayId, moved.GameDayId);

        var gameMove = await client.PostAsJsonAsync($"/api/events/{early}/reschedule", new RescheduleSessionRequest
        {
            NewStart = Start.AddDays(1).AddHours(-2),
            NewEnd = Start.AddDays(1).AddHours(-1),
            Notify = false
        });
        Assert.Equal(HttpStatusCode.BadRequest, gameMove.StatusCode);

        var cancel = await client.PostAsJsonAsync($"/api/game-days/{dayId}/cancel", new CancelSessionRequest
        {
            Reason = "Hall closed",
            Notify = false
        });
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        var day = await client.GetFromJsonAsync<GameDayDto>($"/api/game-days/{dayId}");
        Assert.True(day!.IsCancelled);
        Assert.Equal("Hall closed", day.CancellationReason);
        Assert.All(day.Games, game => Assert.True(game.IsCancelled));
        Assert.Equal(2, day.Games.Count);
    }

    [Fact]
    public async Task Next_card_prefers_an_earlier_game_day()
    {
        var host = await _api.RegisterAndLoginAsync(Email("next-host"), "Next Host");
        using var client = _api.As(host);
        var sooner = DateTimeOffset.UtcNow.AddDays(20);
        var standalone = await client.PostAsJsonAsync("/api/events", new CreateSessionRequest
        {
            Title = "Standalone later",
            ScheduledAt = sooner.AddDays(3),
            EndsAt = sooner.AddDays(3).AddHours(4),
            Venue = "Shop",
            IsPublished = true
        });
        Assert.Equal(HttpStatusCode.Created, standalone.StatusCode);
        var dayId = await CreateDayAsync(client, "Sooner day", sooner);
        await CreateGameAsync(client, dayId, sooner, sooner.AddHours(8), "Table one", follow: true);

        var next = await _api.Client.GetFromJsonAsync<CalendarEventDto>("/api/events/next");
        Assert.Equal("GameDay", next!.ScheduleKind);
        Assert.Equal("Sooner day", next.Title);
        Assert.Contains("Table one", next.RuleSetName);
        Assert.Equal("Front Hall", next.Venue);
    }

    private static string Email(string name) => $"{name}@obwc.test";

    private static string Rsvp(Guid id) => $"/api/events/{id}/rsvp";

    private static CreateGameDayRequest Day(string title = "Saturday games", DateTimeOffset? start = null, int weeks = 1)
    {
        var when = start ?? Start;
        return new CreateGameDayRequest
        {
            Title = title,
            ScheduledAt = when,
            EndsAt = when.AddHours(8),
            Venue = "Front Hall",
            Address = "100 Beach St",
            LocationNotes = "Side door",
            IsPublished = true,
            Weeks = weeks
        };
    }

    private static CreateSessionRequest Game(
        Guid dayId,
        DateTimeOffset start,
        DateTimeOffset? end,
        string title = "Table",
        int? cap = null,
        int weeks = 1,
        bool follow = false,
        string? online = null) =>
        new()
        {
            Title = title,
            ScheduledAt = start,
            EndsAt = end,
            GameDayId = dayId,
            FollowGameDayTimes = follow,
            PlayerCap = cap,
            Weeks = weeks,
            IsPublished = true,
            AllowPlayerRsvp = true,
            OnlineLink = online
        };

    private async Task<Guid> CreateDayAsync(HttpClient client, string title = "Saturday games", DateTimeOffset? start = null)
    {
        var response = await client.PostAsJsonAsync("/api/game-days", Day(title, start));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<CreateSeriesResultDto>();
        return body!.FirstSessionId!.Value;
    }

    private async Task<Guid> CreateGameAsync(
        HttpClient client,
        Guid dayId,
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        string title = "Table",
        int? cap = null,
        string? online = null,
        bool follow = false)
    {
        var when = start ?? Start;
        var response = await client.PostAsJsonAsync("/api/events", Game(dayId, when, end ?? when.AddHours(8), title, cap, follow: follow, online: online));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<CreateSeriesResultDto>();
        return body!.FirstSessionId!.Value;
    }

    private async Task<AuthResponse> LoginAsync(string email)
    {
        var login = await _api.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = ApiTest.Password });
        Assert.True(login.IsSuccessStatusCode, await login.Content.ReadAsStringAsync());
        return (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
    }
}
