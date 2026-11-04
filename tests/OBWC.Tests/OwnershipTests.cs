using System.Net;
using System.Net.Http.Json;
using OBWC.Shared;
using OBWC.Shared.Dtos;

namespace OBWC.Tests;

public class OwnershipTests(ApiFactory factory) : IClassFixture<ApiFactory>, IDisposable
{
    private readonly ApiTest _api = new(factory);

    [Fact]
    public async Task Owners_and_admins_manage_events_other_gms_do_not()
    {
        var member = await _api.RegisterAndLoginAsync("scheduler@obwc.test", "Scheduler");
        var other = await _api.RegisterAndLoginAsync("other@obwc.test", "Other Member");
        await _api.RegisterAndLoginAsync("gm@obwc.test", "Game Master");
        await _api.AddRolesAsync("gm@obwc.test", RoleNames.Gm);
        var gm = await LoginAsync("gm@obwc.test");
        await _api.RegisterAndLoginAsync("events-admin@obwc.test", "Events Admin");
        await _api.AddRolesAsync("events-admin@obwc.test", RoleNames.Admin);
        var admin = await LoginAsync("events-admin@obwc.test");

        var when = DateTimeOffset.UtcNow.AddDays(15);
        var anonymousCreate = await _api.Client.PostAsJsonAsync("/api/events", ApiTest.SampleEvent("Closed table", when));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousCreate.StatusCode);

        using var memberClient = _api.As(member);
        var created = await memberClient.PostAsJsonAsync("/api/events", ApiTest.SampleEvent("Friday Spearhead", when));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdBody = await created.Content.ReadFromJsonAsync<CreateSeriesResultDto>();
        Assert.NotNull(createdBody?.FirstSessionId);
        var game = await memberClient.GetFromJsonAsync<CalendarEventDto>($"/api/events/{createdBody.FirstSessionId}");
        Assert.NotNull(game);
        Assert.True(game.CanEdit);
        Assert.Equal("Scheduler", game.OwnerDisplayName);
        Assert.False(game.OwnerIsGm);

        var badTime = await memberClient.PostAsJsonAsync("/api/events", new CreateSessionRequest
        {
            Title = "Backwards",
            ScheduledAt = when,
            EndsAt = when.AddHours(-1),
            Venue = "Club table"
        });
        Assert.Equal(HttpStatusCode.BadRequest, badTime.StatusCode);

        using var otherClient = _api.As(other);
        var otherEdit = await otherClient.PutAsJsonAsync($"/api/events/{game.Id}", new UpdateSessionDetailsRequest { Title = "Taken over", Venue = "Club table" });
        Assert.Equal(HttpStatusCode.Forbidden, otherEdit.StatusCode);

        using var gmClient = _api.As(gm);
        var gmEdit = await gmClient.PostAsJsonAsync($"/api/events/{game.Id}/reschedule", new RescheduleSessionRequest
        {
            NewStart = when.AddDays(1),
            NewEnd = when.AddDays(1).AddHours(4),
            Notify = false
        });
        Assert.Equal(HttpStatusCode.Forbidden, gmEdit.StatusCode);
        var gmCancel = await gmClient.PostAsJsonAsync($"/api/events/{game.Id}/cancel", new CancelSessionRequest { Reason = "Not my table", Notify = false });
        Assert.Equal(HttpStatusCode.Forbidden, gmCancel.StatusCode);

        var gmGame = await gmClient.PostAsJsonAsync("/api/events", ApiTest.SampleEvent("GM campaign night", when.AddDays(20)));
        Assert.Equal(HttpStatusCode.Created, gmGame.StatusCode);
        var gmCreated = await gmGame.Content.ReadFromJsonAsync<CreateSeriesResultDto>();
        var gmEvent = await gmClient.GetFromJsonAsync<CalendarEventDto>($"/api/events/{gmCreated!.FirstSessionId}");
        Assert.True(gmEvent!.OwnerIsGm);
        var memberTouchesGm = await memberClient.PostAsJsonAsync($"/api/events/{gmEvent.Id}/cancel", new CancelSessionRequest { Notify = false });
        Assert.Equal(HttpStatusCode.Forbidden, memberTouchesGm.StatusCode);

        using var adminClient = _api.As(admin);
        var adminEdit = await adminClient.PutAsJsonAsync($"/api/events/{game.Id}", new UpdateSessionDetailsRequest
        {
            Title = "Friday Spearhead (moved)",
            Venue = game.Venue,
            Address = game.Address,
            Notes = game.Notes
        });
        Assert.Equal(HttpStatusCode.OK, adminEdit.StatusCode);
        var edited = await adminEdit.Content.ReadFromJsonAsync<CalendarEventDto>();
        Assert.Equal("Friday Spearhead (moved)", edited!.Title);
        Assert.Equal(member.User.Id, edited.OwnerUserId);

        var rsvp = await otherClient.PutAsJsonAsync($"/api/events/{game.Id}/rsvp", new SetRsvpRequest { Status = SessionRsvpStatus.Going });
        Assert.Equal(HttpStatusCode.OK, rsvp.StatusCode);
        var publicView = await _api.Client.GetFromJsonAsync<CalendarEventDto>($"/api/events/{game.Id}");
        Assert.Equal(1, publicView!.Going);
        Assert.Equal(SessionRsvpStatus.Unknown, publicView.MyRsvp);
        var publicRoster = await _api.Client.GetAsync($"/api/events/{game.Id}/roster");
        Assert.Equal(HttpStatusCode.Unauthorized, publicRoster.StatusCode);
        var memberRoster = await memberClient.GetFromJsonAsync<List<RosterRowDto>>($"/api/events/{game.Id}/roster");
        Assert.Contains(memberRoster!, row => row.DisplayName == "Other Member" && row.Rsvp == SessionRsvpStatus.Going);

        var next = await _api.Client.GetFromJsonAsync<CalendarEventDto>("/api/events/next");
        Assert.NotNull(next);
        Assert.False(next.IsCancelled);
    }

    [Fact]
    public async Task Members_own_only_their_rule_set_entries()
    {
        var member = await _api.RegisterAndLoginAsync("rules@obwc.test", "Rules Member");
        var other = await _api.RegisterAndLoginAsync("rules-other@obwc.test", "Rules Other");
        await _api.RegisterAndLoginAsync("rules-admin@obwc.test", "Rules Admin");
        await _api.AddRolesAsync("rules-admin@obwc.test", RoleNames.Admin);
        var admin = await LoginAsync("rules-admin@obwc.test");

        var ruleSets = await _api.Client.GetFromJsonAsync<List<RuleSetDto>>("/api/rulesets");
        var flames = ruleSets!.Single(ruleSet => ruleSet.Name == "Flames of War");

        using var memberClient = _api.As(member);
        var rename = await memberClient.PutAsJsonAsync($"/api/rulesets/{flames.Id}", new SaveRuleSetRequest
        {
            Name = "Not the published name",
            PublishedPlayers = flames.PublishedPlayers
        });
        Assert.Equal(HttpStatusCode.Forbidden, rename.StatusCode);

        var added = await memberClient.PostAsJsonAsync($"/api/rulesets/{flames.Id}/players", new SavePlayerRequest { Note = "Weeknights" });
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        var duplicate = await memberClient.PostAsJsonAsync($"/api/rulesets/{flames.Id}/players", new SavePlayerRequest());
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var updated = await memberClient.GetFromJsonAsync<RuleSetDto>($"/api/rulesets/{flames.Id}");
        var mine = updated!.Players.Single(player => player.OwnerUserId == member.User.Id);
        Assert.True(mine.CanEdit);
        Assert.Equal("Rules Member", mine.DisplayName);
        Assert.Contains("Doug Carmichael", updated.PublishedPlayers, StringComparison.Ordinal);

        using var otherClient = _api.As(other);
        var stolen = await otherClient.DeleteAsync($"/api/rulesets/{flames.Id}/players/{mine.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, stolen.StatusCode);

        using var adminClient = _api.As(admin);
        var removed = await adminClient.DeleteAsync($"/api/rulesets/{flames.Id}/players/{mine.Id}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
    }

    private async Task<AuthResponse> LoginAsync(string email)
    {
        var response = await _api.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = ApiTest.Password
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    public void Dispose() => _api.Dispose();
}
