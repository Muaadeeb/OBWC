using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using OBWC.Shared;
using OBWC.Shared.Dtos;

namespace OBWC.Tests;

public class ContentAuthorizationTests(ApiFactory factory) : IClassFixture<ApiFactory>, IDisposable
{
    private readonly ApiTest _api = new(factory);

    private static readonly byte[] TinyJpeg = [0xFF, 0xD8, 0xFF, 0xD9];

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

    [Fact]
    public async Task Admin_can_set_replace_and_remove_page_photos()
    {
        var member = await _api.RegisterAndLoginAsync("page-photo-member@obwc.test", "Page Photo Member");
        await _api.RegisterAndLoginAsync("page-photo-admin@obwc.test", "Page Photo Admin");
        await _api.AddRolesAsync("page-photo-admin@obwc.test", RoleNames.Admin);
        var login = await _api.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "page-photo-admin@obwc.test",
            Password = ApiTest.Password
        });
        var admin = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        using var memberClient = _api.As(member);
        using var adminClient = _api.As(admin);

        Assert.Equal(HttpStatusCode.NotFound, (await _api.Client.GetAsync("/api/content/home")).StatusCode);
        using (var memberPhoto = ImageUpload(TinyPng, "home.png", "image/png"))
            Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.PostAsync("/api/content/home/photo", memberPhoto)).StatusCode);
        using (var nuke = ImageUpload(TinyPng, "nuke.png", "image/png"))
            Assert.Equal(HttpStatusCode.NotFound, (await adminClient.PostAsync("/api/content/nuke-con/photo", nuke)).StatusCode);

        using var first = ImageUpload(TinyPng, "home.png", "image/png");
        var uploaded = await adminClient.PostAsync("/api/content/home/photo", first);
        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        var home = (await uploaded.Content.ReadFromJsonAsync<ContentPageDto>())!;
        Assert.Contains("?v=", home.PhotoUrl, StringComparison.Ordinal);
        Assert.Equal(TinyPng, await _api.Client.GetByteArrayAsync(home.PhotoUrl));

        using var second = ImageUpload(TinyJpeg, "home.jpg", "image/jpeg");
        var replaced = await adminClient.PostAsync("/api/content/home/photo", second);
        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        var next = (await replaced.Content.ReadFromJsonAsync<ContentPageDto>())!;
        Assert.NotEqual(home.PhotoUrl, next.PhotoUrl);
        Assert.Equal(TinyJpeg, await _api.Client.GetByteArrayAsync(next.PhotoUrl));

        var about = await _api.Client.GetFromJsonAsync<ContentPageDto>("/api/content/about");
        using var aboutPhoto = ImageUpload(TinyPng, "about.png", "image/png");
        var aboutUploaded = await adminClient.PostAsync("/api/content/about/photo", aboutPhoto);
        Assert.Equal(HttpStatusCode.OK, aboutUploaded.StatusCode);
        var withPhoto = (await aboutUploaded.Content.ReadFromJsonAsync<ContentPageDto>())!;
        var saved = await adminClient.PutAsJsonAsync("/api/content/about", new SaveContentRequest
        {
            Title = about!.Title,
            Body = about.Body
        });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var still = (await saved.Content.ReadFromJsonAsync<ContentPageDto>())!;
        Assert.Equal(withPhoto.PhotoUrl, still.PhotoUrl);
        Assert.Contains("promote historical and miniature gaming", still.Body, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.DeleteAsync("/api/content/about/photo")).StatusCode);
        var cleared = await _api.Client.GetFromJsonAsync<ContentPageDto>("/api/content/about");
        Assert.Null(cleared!.PhotoUrl);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.Client.GetAsync(withPhoto.PhotoUrl)).StatusCode);
    }

    private static MultipartFormDataContent ImageUpload(byte[] body, string fileName, string contentType)
    {
        var content = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(body);
        bytes.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(bytes, "file", fileName);
        return content;
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
