using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using OBWC.Shared;
using OBWC.Shared.Dtos;

namespace OBWC.Tests;

public class ContentAuthorizationTests(ApiFactory factory) : IClassFixture<ApiFactory>, IDisposable
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
    public async Task Club_content_stores_documents_and_gallery_follow_role_rules()
    {
        var member = await _api.RegisterAndLoginAsync("content-member@obwc.test", "Content Member");
        await _api.RegisterAndLoginAsync("content-admin@obwc.test", "Content Admin");
        await _api.AddRolesAsync("content-admin@obwc.test", RoleNames.Admin);
        var adminLogin = await _api.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "content-admin@obwc.test",
            Password = ApiTest.Password
        });
        var admin = (await adminLogin.Content.ReadFromJsonAsync<AuthResponse>())!;

        using var memberClient = _api.As(member);
        var memberStore = await memberClient.PostAsJsonAsync("/api/stores", new SaveStoreRequest { Name = "Not allowed" });
        Assert.Equal(HttpStatusCode.Forbidden, memberStore.StatusCode);

        using var adminClient = _api.As(admin);
        var created = await adminClient.PostAsJsonAsync("/api/stores", new SaveStoreRequest
        {
            Name = "Example Shelf",
            Address = "Entered by an admin during a test",
            Bullet1 = "Open tables",
            SortOrder = 5
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var stores = await _api.Client.GetFromJsonAsync<List<StoreDto>>("/api/stores");
        Assert.Contains(stores!, store => store.Name == "Example Shelf");
        Assert.Contains(stores!, store => store.Name == "Rad Comics and Collectibles");

        var anonymousDocs = await _api.Client.GetAsync("/api/documents?category=Constitution");
        Assert.Equal(HttpStatusCode.OK, anonymousDocs.StatusCode);
        Assert.Empty((await anonymousDocs.Content.ReadFromJsonAsync<List<ClubDocumentDto>>())!);
        var memberDocs = await memberClient.GetFromJsonAsync<List<ClubDocumentDto>>("/api/documents?category=Constitution");
        Assert.Empty(memberDocs!);

        using var upload = PdfUpload("constitution.pdf");
        var memberUpload = await memberClient.PostAsync("/api/documents", upload);
        Assert.Equal(HttpStatusCode.Forbidden, memberUpload.StatusCode);

        using var adminUpload = PdfUpload("constitution.pdf");
        var uploaded = await adminClient.PostAsync("/api/documents", adminUpload);
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        var document = await uploaded.Content.ReadFromJsonAsync<ClubDocumentDto>();
        var download = await memberClient.GetAsync($"/api/documents/{document!.Id}/file");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Contains("Club constitution draft.", await download.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        var publicDownload = await _api.Client.GetAsync($"/api/documents/{document.Id}/file");
        Assert.Equal(HttpStatusCode.Unauthorized, publicDownload.StatusCode);

        var contact = await _api.Client.PostAsJsonAsync("/api/contact", new ContactRequest
        {
            Name = "Visitor",
            Email = "visitor@example.com",
            Message = "When is the next game day?"
        });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, contact.StatusCode);
        Assert.Contains("nmh16055@gmail.com", await contact.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var gallery = await _api.Client.GetFromJsonAsync<GalleryListDto>("/api/gallery");
        Assert.Equal(2, gallery!.MaxPhotos);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.Client.PostAsync("/api/gallery", Photo("One", "Nuke-Con"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.PostAsync("/api/gallery", Photo("Two", "Nuke-Con"))).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await adminClient.PostAsync("/api/gallery", Photo("Table one", "Nuke-Con"))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await adminClient.PostAsync("/api/gallery", Photo("Table two", "ScroogeCon"))).StatusCode);
        var third = await adminClient.PostAsync("/api/gallery", Photo("Table three", "Game day"));
        Assert.Equal(HttpStatusCode.Conflict, third.StatusCode);
        var full = await _api.Client.GetFromJsonAsync<GalleryListDto>("/api/gallery");
        Assert.Equal(2, full!.Count);
        Assert.Equal(100, GalleryLimits.MaxPhotos);
    }

    private static MultipartFormDataContent PdfUpload(string fileName)
    {
        var content = new MultipartFormDataContent();
        var bytes = new ByteArrayContent("%PDF-1.4\nClub constitution draft.\n%%EOF"u8.ToArray());
        bytes.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(bytes, "file", fileName);
        content.Add(new StringContent("OBWC Constitution"), "title");
        content.Add(new StringContent("Constitution"), "category");
        return content;
    }

    private static MultipartFormDataContent Photo(string caption, string publicEvent)
    {
        var content = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(TinyPng);
        bytes.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(bytes, "file", "game.png");
        content.Add(new StringContent(caption), "caption");
        content.Add(new StringContent(publicEvent), "publicEventName");
        return content;
    }

    public void Dispose() => _api.Dispose();
}
