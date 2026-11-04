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
