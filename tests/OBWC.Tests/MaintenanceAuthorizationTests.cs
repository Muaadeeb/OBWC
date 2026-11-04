using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using OBWC.Shared;
using OBWC.Shared.Dtos;

namespace OBWC.Tests;

public class MaintenanceAuthorizationTests(ApiFactory factory) : IClassFixture<ApiFactory>, IDisposable
{
    private readonly ApiTest _api = new(factory);

    private static readonly byte[] TinyPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82
    ];

    [Fact]
    public async Task Members_cannot_maintain_club_content_admins_can()
    {
        var member = await _api.RegisterAndLoginAsync("maint-member@obwc.test", "Maint Member");
        await _api.RegisterAndLoginAsync("maint-admin@obwc.test", "Maint Admin");
        await _api.AddRolesAsync("maint-admin@obwc.test", RoleNames.Admin);
        var admin = await LoginAsync("maint-admin@obwc.test");

        using var memberClient = _api.As(member);
        using var adminClient = _api.As(admin);

        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.PostAsJsonAsync("/api/officers", Officer("President"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.PostAsJsonAsync("/api/conventions", new SaveConventionRequest { Name = "Hidden" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.PostAsJsonAsync("/api/groups", new SaveGroupRequest { Name = "Hidden group" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.PostAsJsonAsync("/api/venues", new SaveVenueRequest { Name = "Hidden hall" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.PostAsync("/api/gallery", Photo("Nope", "Game day"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.PostAsync("/api/documents", Pdf("notes.pdf", DocumentVisibility.Public))).StatusCode);

        var officer = await adminClient.PostAsJsonAsync("/api/officers", Officer("Secretary"));
        Assert.Equal(HttpStatusCode.Created, officer.StatusCode);
        var createdOfficer = await officer.Content.ReadFromJsonAsync<OfficerDto>();
        Assert.Equal("Secretary", createdOfficer!.RoleTitle);
        Assert.Equal(2, createdOfficer.SortOrder);

        var convention = await adminClient.PostAsJsonAsync("/api/conventions", new SaveConventionRequest
        {
            Name = "Test Con",
            StartsOn = new DateOnly(2026, 6, 1),
            EndsOn = new DateOnly(2026, 6, 3),
            LocationText = "Omaha",
            WebsiteUrl = "https://example.com/con",
            Description = "A weekend of games."
        });
        Assert.Equal(HttpStatusCode.Created, convention.StatusCode);

        var backwards = await adminClient.PostAsJsonAsync("/api/conventions", new SaveConventionRequest
        {
            Name = "Backwards",
            StartsOn = new DateOnly(2026, 7, 4),
            EndsOn = new DateOnly(2026, 7, 1)
        });
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);

        using var pdf = Pdf("minutes.pdf", DocumentVisibility.MembersOnly);
        var uploaded = await adminClient.PostAsync("/api/documents", pdf);
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        var document = await uploaded.Content.ReadFromJsonAsync<ClubDocumentDto>();
        Assert.Equal(DocumentVisibility.MembersOnly, document!.Visibility);
        Assert.Equal(DocumentCategory.Minutes, document.Category);

        var guestList = await _api.Client.GetFromJsonAsync<List<ClubDocumentDto>>("/api/documents");
        Assert.DoesNotContain(guestList!, item => item.Id == document.Id);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.Client.GetAsync($"/api/documents/{document.Id}/file")).StatusCode);

        var renamedByMember = await memberClient.PutAsJsonAsync($"/api/documents/{document.Id}", new RenameDocumentRequest
        {
            Title = "Taken",
            Visibility = DocumentVisibility.Public
        });
        Assert.Equal(HttpStatusCode.Forbidden, renamedByMember.StatusCode);

        var renamed = await adminClient.PutAsJsonAsync($"/api/documents/{document.Id}", new RenameDocumentRequest
        {
            Title = "June minutes",
            Category = DocumentCategory.Minutes,
            Visibility = DocumentVisibility.Public
        });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var guestAfter = await _api.Client.GetFromJsonAsync<List<ClubDocumentDto>>("/api/documents?category=Minutes");
        Assert.Contains(guestAfter!, item => item.Title == "June minutes" && item.Visibility == DocumentVisibility.Public);
        Assert.Equal(HttpStatusCode.OK, (await _api.Client.GetAsync($"/api/documents/{document.Id}/file")).StatusCode);

        using var text = new MultipartFormDataContent();
        var plain = new ByteArrayContent("not a pdf"u8.ToArray());
        plain.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        text.Add(plain, "file", "minutes.txt");
        text.Add(new StringContent("Notes"), "title");
        text.Add(new StringContent("Minutes"), "category");
        Assert.Equal(HttpStatusCode.BadRequest, (await adminClient.PostAsync("/api/documents", text)).StatusCode);
    }

    [Fact]
    public async Task Gm_manages_own_event_photos_and_venues_not_another_gms()
    {
        var member = await _api.RegisterAndLoginAsync("photo-member@obwc.test", "Photo Member");
        await _api.RegisterAndLoginAsync("photo-gm@obwc.test", "Photo GM");
        await _api.AddRolesAsync("photo-gm@obwc.test", RoleNames.Gm);
        var gm = await LoginAsync("photo-gm@obwc.test");
        await _api.RegisterAndLoginAsync("photo-other-gm@obwc.test", "Other GM");
        await _api.AddRolesAsync("photo-other-gm@obwc.test", RoleNames.Gm);
        var otherGm = await LoginAsync("photo-other-gm@obwc.test");
        await _api.RegisterAndLoginAsync("photo-admin@obwc.test", "Photo Admin");
        await _api.AddRolesAsync("photo-admin@obwc.test", RoleNames.Admin);
        var admin = await LoginAsync("photo-admin@obwc.test");

        var when = DateTimeOffset.UtcNow.AddDays(12);
        using var memberClient = _api.As(member);
        using var gmClient = _api.As(gm);
        using var otherClient = _api.As(otherGm);
        using var adminClient = _api.As(admin);

        var memberEvent = await memberClient.PostAsJsonAsync("/api/events", ApiTest.SampleEvent("Member table", when));
        var memberCreated = await memberEvent.Content.ReadFromJsonAsync<CreateSeriesResultDto>();
        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.PostAsync($"/api/events/{memberCreated!.FirstSessionId}/photos", PhotoForm("Mine"))).StatusCode);

        var gmEvent = await gmClient.PostAsJsonAsync("/api/events", ApiTest.SampleEvent("GM table", when.AddDays(1)));
        Assert.Equal(HttpStatusCode.Created, gmEvent.StatusCode);
        var gmCreated = await gmEvent.Content.ReadFromJsonAsync<CreateSeriesResultDto>();
        var eventId = gmCreated!.FirstSessionId;

        var stolenEdit = await otherClient.PutAsJsonAsync($"/api/events/{eventId}", new UpdateSessionDetailsRequest { Title = "Taken", Venue = "Elsewhere" });
        Assert.Equal(HttpStatusCode.Forbidden, stolenEdit.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherClient.PostAsync($"/api/events/{eventId}/photos", PhotoForm("Not mine"))).StatusCode);

        var uploaded = await gmClient.PostAsync($"/api/events/{eventId}/photos", PhotoForm("Table shot"));
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        var photo = await uploaded.Content.ReadFromJsonAsync<EventPhotoDto>();

        Assert.Equal(HttpStatusCode.Forbidden, (await otherClient.PutAsJsonAsync($"/api/events/{eventId}/photos/{photo!.Id}", new UpdateEventPhotoRequest { Caption = "Stolen" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherClient.DeleteAsync($"/api/events/{eventId}/photos/{photo.Id}")).StatusCode);

        var adminEdit = await adminClient.PutAsJsonAsync($"/api/events/{eventId}", new UpdateSessionDetailsRequest { Title = "GM table (admin)", Venue = "Club table" });
        Assert.Equal(HttpStatusCode.OK, adminEdit.StatusCode);
        var adminPhoto = await adminClient.PostAsync($"/api/events/{eventId}/photos", PhotoForm("Admin shot"));
        Assert.Equal(HttpStatusCode.Created, adminPhoto.StatusCode);
        var adminShot = await adminPhoto.Content.ReadFromJsonAsync<EventPhotoDto>();
        Assert.Equal(HttpStatusCode.Forbidden, (await gmClient.DeleteAsync($"/api/events/{eventId}/photos/{adminShot!.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.DeleteAsync($"/api/events/{eventId}/photos/{photo.Id}")).StatusCode);

        var listed = await _api.Client.GetFromJsonAsync<CalendarEventDto>($"/api/events/{eventId}");
        Assert.Contains(listed!.Photos, item => item.Id == adminShot.Id);
        Assert.DoesNotContain(listed.Photos, item => item.Id == photo.Id);
        Assert.False(listed.CanAddPhotos);

        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.PostAsJsonAsync("/api/venues", new SaveVenueRequest { Name = "Side room" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await gmClient.PostAsync("/api/gallery", PhotoForm("Gallery"))).StatusCode);
        var venue = await gmClient.PostAsJsonAsync("/api/venues", new SaveVenueRequest
        {
            Name = "Side room",
            Address = "1 Club Lane",
            Description = "Back tables",
            SortOrder = 3
        });
        Assert.Equal(HttpStatusCode.Created, venue.StatusCode);
        var saved = await venue.Content.ReadFromJsonAsync<VenueDto>();
        var edited = await gmClient.PutAsJsonAsync($"/api/venues/{saved!.Id}", new SaveVenueRequest
        {
            Name = "Side room",
            Address = "1 Club Lane",
            Description = "Notes from the GM",
            SortOrder = 3
        });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var publicVenues = await _api.Client.GetFromJsonAsync<List<VenueDto>>("/api/venues");
        Assert.Contains(publicVenues!, item => item.Name == "Side room" && item.Description == "Notes from the GM");
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

    private static SaveOfficerRequest Officer(string title) => new()
    {
        RoleTitle = title,
        DisplayName = "Elected member",
        TermLabel = "2026–2027",
        Contact = "board@example.com",
        SortOrder = 2
    };

    private static MultipartFormDataContent Pdf(string fileName, DocumentVisibility visibility)
    {
        var content = new MultipartFormDataContent();
        var bytes = new ByteArrayContent("%PDF-1.4\nJune minutes\n%%EOF"u8.ToArray());
        bytes.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(bytes, "file", fileName);
        content.Add(new StringContent("Minutes"), "title");
        content.Add(new StringContent(nameof(DocumentCategory.Minutes)), "category");
        content.Add(new StringContent(visibility.ToString()), "visibility");
        return content;
    }

    private static MultipartFormDataContent Photo(string caption, string publicEvent)
    {
        var content = PhotoForm(caption);
        content.Add(new StringContent(publicEvent), "publicEventName");
        return content;
    }

    private static MultipartFormDataContent PhotoForm(string caption)
    {
        var content = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(TinyPng);
        bytes.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(bytes, "file", "game.png");
        content.Add(new StringContent(caption), "caption");
        return content;
    }

    public void Dispose() => _api.Dispose();
}
