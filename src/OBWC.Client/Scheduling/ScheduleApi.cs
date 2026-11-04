using System.Net;
using System.Net.Http.Json;
using OBWC.Client.Services;
using OBWC.Shared;
using OBWC.Shared.Dtos;

namespace OBWC.Client.Scheduling;

/// <summary>
/// HTTP client for the game-schedule module only.
/// </summary>
public sealed class ScheduleApi(HttpClient http)
{
    public async Task<IReadOnlyList<CalendarEventDto>> ListAsync(int? year = null, int? month = null, CancellationToken cancellationToken = default)
    {
        var url = year is int y && month is int m ? $"api/events?year={y}&month={m}" : "api/events";
        var items = await http.GetFromJsonAsync<List<CalendarEventDto>>(url, JsonDefaults.Options, cancellationToken);
        return items ?? [];
    }

    public async Task<CalendarEventDto?> GetNextAsync(CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync("api/events/next", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        await EnsureAsync(response);
        return await response.Content.ReadFromJsonAsync<CalendarEventDto>(JsonDefaults.Options, cancellationToken);
    }

    public async Task<IReadOnlyList<CalendarEventDto>> MineAsync(CancellationToken cancellationToken = default)
    {
        var items = await http.GetFromJsonAsync<List<CalendarEventDto>>("api/events/mine", JsonDefaults.Options, cancellationToken);
        return items ?? [];
    }

    public Task<CreateSeriesResultDto> CreateAsync(CreateSessionRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<CreateSeriesResultDto>(HttpMethod.Post, "api/events", request, cancellationToken);

    public Task<CalendarEventDto> UpdateAsync(Guid id, UpdateSessionDetailsRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<CalendarEventDto>(HttpMethod.Put, $"api/events/{id}", request, cancellationToken);

    public Task PublishAsync(Guid id, bool published, CancellationToken cancellationToken = default) =>
        SendEmptyAsync(HttpMethod.Post, $"api/events/{id}/publish", new PublishSessionRequest { Published = published }, cancellationToken);

    public Task CancelAsync(Guid id, CancelSessionRequest request, CancellationToken cancellationToken = default) =>
        SendEmptyAsync(HttpMethod.Post, $"api/events/{id}/cancel", request, cancellationToken);

    public Task RescheduleAsync(Guid id, RescheduleSessionRequest request, CancellationToken cancellationToken = default) =>
        SendEmptyAsync(HttpMethod.Post, $"api/events/{id}/reschedule", request, cancellationToken);

    public Task<RsvpResultDto> RsvpAsync(Guid id, SessionRsvpStatus status, CancellationToken cancellationToken = default) =>
        SendAsync<RsvpResultDto>(HttpMethod.Put, $"api/events/{id}/rsvp", new SetRsvpRequest { Status = status }, cancellationToken);

    public Task CheckInAsync(Guid id, string? userId, CancellationToken cancellationToken = default) =>
        SendEmptyAsync(HttpMethod.Post, $"api/events/{id}/check-in", new CheckInRequest { UserId = userId }, cancellationToken);

    public Task UndoCheckInAsync(Guid id, string? userId, CancellationToken cancellationToken = default) =>
        SendEmptyAsync(HttpMethod.Post, $"api/events/{id}/check-in/undo", new CheckInRequest { UserId = userId }, cancellationToken);

    public async Task<IReadOnlyList<RosterRowDto>> RosterAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var items = await http.GetFromJsonAsync<List<RosterRowDto>>($"api/events/{id}/roster", JsonDefaults.Options, cancellationToken);
        return items ?? [];
    }

    public async Task<byte[]> DownloadIcsAsync(CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync("api/events/calendar.ics", cancellationToken);
        await EnsureAsync(response);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string url, object body, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body, options: JsonDefaults.Options) };
        var response = await http.SendAsync(request, cancellationToken);
        await EnsureAsync(response);
        return await response.Content.ReadFromJsonAsync<T>(JsonDefaults.Options, cancellationToken)
            ?? throw new ClubApiException(response.StatusCode, "The server returned an empty response.");
    }

    private async Task SendEmptyAsync(HttpMethod method, string url, object body, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body, options: JsonDefaults.Options) };
        var response = await http.SendAsync(request, cancellationToken);
        await EnsureAsync(response);
    }

    private static async Task EnsureAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new ClubApiException(response.StatusCode, await ApiErrors.ReadAsync(response));
    }
}
