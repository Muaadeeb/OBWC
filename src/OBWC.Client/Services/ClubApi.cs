using System.Net.Http.Headers;
using System.Net.Http.Json;
using OBWC.Shared;
using OBWC.Shared.Dtos;

namespace OBWC.Client.Services;

public sealed class ClubApi(HttpClient http)
{
    public Task<PublicInfoDto?> GetPublicInfoAsync(CancellationToken cancellationToken = default) =>
        GetAsync<PublicInfoDto>("api/settings/public", cancellationToken);

    public Task<PublicInfoDto> UpdatePublicInfoAsync(UpdatePublicInfoRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<PublicInfoDto>(HttpMethod.Put, "api/settings/public", request, cancellationToken);

    public Task<ContentPageDto?> GetContentAsync(string key, CancellationToken cancellationToken = default) =>
        GetAsync<ContentPageDto>($"api/content/{key}", cancellationToken);

    public Task<ContentPageDto> UpdateContentAsync(string key, SaveContentRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<ContentPageDto>(HttpMethod.Put, $"api/content/{key}", request, cancellationToken);

    public Task<IReadOnlyList<OfficerDto>> GetOfficersAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<OfficerDto>("api/officers", cancellationToken);

    public Task<OfficerDto> CreateOfficerAsync(SaveOfficerRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<OfficerDto>(HttpMethod.Post, "api/officers", request, cancellationToken);

    public Task<OfficerDto> UpdateOfficerAsync(Guid id, SaveOfficerRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<OfficerDto>(HttpMethod.Put, $"api/officers/{id}", request, cancellationToken);

    public Task DeleteOfficerAsync(Guid id, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/officers/{id}", cancellationToken);

    public async Task<OfficerDto> UploadOfficerPhotoAsync(Guid id, byte[] content, string fileName, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        form.Add(FileContent(content, "image/png"), "file", fileName);
        var response = await http.PostAsync($"api/officers/{id}/photo", form, cancellationToken);
        return await ReadAsync<OfficerDto>(response, cancellationToken);
    }

    public Task<IReadOnlyList<StoreDto>> GetStoresAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<StoreDto>("api/stores", cancellationToken);

    public Task<StoreDto> CreateStoreAsync(SaveStoreRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<StoreDto>(HttpMethod.Post, "api/stores", request, cancellationToken);

    public Task<StoreDto> UpdateStoreAsync(Guid id, SaveStoreRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<StoreDto>(HttpMethod.Put, $"api/stores/{id}", request, cancellationToken);

    public Task DeleteStoreAsync(Guid id, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/stores/{id}", cancellationToken);

    public Task<IReadOnlyList<VenueDto>> GetVenuesAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<VenueDto>("api/venues", cancellationToken);

    public Task<VenueDto> CreateVenueAsync(SaveVenueRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<VenueDto>(HttpMethod.Post, "api/venues", request, cancellationToken);

    public Task<VenueDto> UpdateVenueAsync(Guid id, SaveVenueRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<VenueDto>(HttpMethod.Put, $"api/venues/{id}", request, cancellationToken);

    public Task DeleteVenueAsync(Guid id, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/venues/{id}", cancellationToken);

    public async Task<VenueDto> UploadVenuePhotosAsync(Guid id, byte[]? inside, string? insideName, byte[]? outside, string? outsideName, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        if (inside is not null)
            form.Add(FileContent(inside, "image/png"), "inside", insideName ?? "inside.png");
        if (outside is not null)
            form.Add(FileContent(outside, "image/png"), "outside", outsideName ?? "outside.png");
        var response = await http.PostAsync($"api/venues/{id}/photos", form, cancellationToken);
        return await ReadAsync<VenueDto>(response, cancellationToken);
    }

    public Task<IReadOnlyList<GroupDto>> GetGroupsAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<GroupDto>("api/groups", cancellationToken);

    public Task<GroupDto> CreateGroupAsync(SaveGroupRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<GroupDto>(HttpMethod.Post, "api/groups", request, cancellationToken);

    public Task<GroupDto> UpdateGroupAsync(Guid id, SaveGroupRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<GroupDto>(HttpMethod.Put, $"api/groups/{id}", request, cancellationToken);

    public Task DeleteGroupAsync(Guid id, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/groups/{id}", cancellationToken);

    public Task<IReadOnlyList<ConventionDto>> GetConventionsAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<ConventionDto>("api/conventions", cancellationToken);

    public Task<ConventionDto> CreateConventionAsync(SaveConventionRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<ConventionDto>(HttpMethod.Post, "api/conventions", request, cancellationToken);

    public Task<ConventionDto> UpdateConventionAsync(Guid id, SaveConventionRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<ConventionDto>(HttpMethod.Put, $"api/conventions/{id}", request, cancellationToken);

    public Task DeleteConventionAsync(Guid id, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/conventions/{id}", cancellationToken);

    public Task<IReadOnlyList<RuleSetDto>> GetRuleSetsAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<RuleSetDto>("api/rulesets", cancellationToken);

    public Task<RuleSetDto> SaveRuleSetAsync(Guid? id, SaveRuleSetRequest request, CancellationToken cancellationToken = default) =>
        id is null
            ? SendAsync<RuleSetDto>(HttpMethod.Post, "api/rulesets", request, cancellationToken)
            : SendAsync<RuleSetDto>(HttpMethod.Put, $"api/rulesets/{id}", request, cancellationToken);

    public Task DeleteRuleSetAsync(Guid id, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/rulesets/{id}", cancellationToken);

    public Task<RuleSetDto> AddPlayerAsync(Guid ruleSetId, SavePlayerRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<RuleSetDto>(HttpMethod.Post, $"api/rulesets/{ruleSetId}/players", request, cancellationToken);

    public Task<RuleSetDto> UpdatePlayerAsync(Guid ruleSetId, Guid playerId, SavePlayerRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<RuleSetDto>(HttpMethod.Put, $"api/rulesets/{ruleSetId}/players/{playerId}", request, cancellationToken);

    public Task DeletePlayerAsync(Guid ruleSetId, Guid playerId, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/rulesets/{ruleSetId}/players/{playerId}", cancellationToken);

    public Task<GalleryListDto?> GetGalleryAsync(CancellationToken cancellationToken = default) =>
        GetAsync<GalleryListDto>("api/gallery", cancellationToken);

    public async Task<GalleryPhotoDto> UploadPhotoAsync(string caption, string publicEventName, byte[] content, string fileName, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        form.Add(FileContent(content, "application/octet-stream"), "file", fileName);
        form.Add(new StringContent(caption), "caption");
        form.Add(new StringContent(publicEventName), "publicEventName");
        var response = await http.PostAsync("api/gallery", form, cancellationToken);
        return await ReadAsync<GalleryPhotoDto>(response, cancellationToken);
    }

    public Task DeletePhotoAsync(Guid id, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/gallery/{id}", cancellationToken);

    public Task<GalleryPhotoDto> UpdatePhotoAsync(Guid id, UpdateGalleryPhotoRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<GalleryPhotoDto>(HttpMethod.Put, $"api/gallery/{id}", request, cancellationToken);

    public async Task<EventPhotoDto> UploadEventPhotoAsync(Guid eventId, string caption, byte[] content, string fileName, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        form.Add(FileContent(content, "application/octet-stream"), "file", fileName);
        form.Add(new StringContent(caption), "caption");
        var response = await http.PostAsync($"api/events/{eventId}/photos", form, cancellationToken);
        return await ReadAsync<EventPhotoDto>(response, cancellationToken);
    }

    public Task<EventPhotoDto> UpdateEventPhotoAsync(Guid eventId, Guid photoId, string caption, CancellationToken cancellationToken = default) =>
        SendAsync<EventPhotoDto>(HttpMethod.Put, $"api/events/{eventId}/photos/{photoId}", new UpdateEventPhotoRequest { Caption = caption }, cancellationToken);

    public Task DeleteEventPhotoAsync(Guid eventId, Guid photoId, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/events/{eventId}/photos/{photoId}", cancellationToken);

    public Task<IReadOnlyList<ClubDocumentDto>> GetDocumentsAsync(DocumentCategory? category = null, CancellationToken cancellationToken = default)
    {
        var url = category is null ? "api/documents" : $"api/documents?category={category}";
        return GetListAsync<ClubDocumentDto>(url, cancellationToken);
    }

    public async Task<ClubDocumentDto> UploadDocumentAsync(
        DocumentCategory category,
        string title,
        string? description,
        DocumentVisibility visibility,
        byte[] content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        form.Add(FileContent(content, "application/pdf"), "file", fileName);
        form.Add(new StringContent(title), "title");
        form.Add(new StringContent(category.ToString()), "category");
        form.Add(new StringContent(visibility.ToString()), "visibility");
        if (!string.IsNullOrWhiteSpace(description))
            form.Add(new StringContent(description), "description");
        var response = await http.PostAsync("api/documents", form, cancellationToken);
        return await ReadAsync<ClubDocumentDto>(response, cancellationToken);
    }

    public Task<ClubDocumentDto> RenameDocumentAsync(Guid id, RenameDocumentRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<ClubDocumentDto>(HttpMethod.Put, $"api/documents/{id}", request, cancellationToken);

    public async Task<(byte[] Bytes, string FileName)> DownloadDocumentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync($"api/documents/{id}/file", cancellationToken);
        await EnsureAsync(response);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var name = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
            ?? "document";
        return (bytes, name);
    }

    public Task DeleteDocumentAsync(Guid id, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/documents/{id}", cancellationToken);

    public Task<IReadOnlyList<UserSummaryDto>> GetUsersAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<UserSummaryDto>("api/users", cancellationToken);

    public Task<UserSummaryDto> UpdateRolesAsync(string id, UpdateRolesRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<UserSummaryDto>(HttpMethod.Put, $"api/users/{id}/roles", request, cancellationToken);

    public Task<IReadOnlyList<DevMailMessageDto>> GetDevMailAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<DevMailMessageDto>("api/dev/mail", cancellationToken);

    public async Task<string?> SendContactAsync(ContactRequest request, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/contact", request, JsonDefaults.Options, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<MessageResponse>(JsonDefaults.Options, cancellationToken);
            return body?.Message ?? "Thanks. Your note was sent.";
        }

        throw new ClubApiException(response.StatusCode, await ApiErrors.ReadAsync(response));
    }

    private async Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken)
    {
        var response = await http.GetAsync(url, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return default;
        return await ReadAsync<T>(response, cancellationToken);
    }

    private async Task<IReadOnlyList<T>> GetListAsync<T>(string url, CancellationToken cancellationToken)
    {
        var items = await GetAsync<List<T>>(url, cancellationToken);
        return items ?? [];
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string url, object body, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, url)
        {
            Content = JsonContent.Create(body, options: JsonDefaults.Options)
        };
        var response = await http.SendAsync(request, cancellationToken);
        return await ReadAsync<T>(response, cancellationToken);
    }

    private async Task DeleteAsync(string url, CancellationToken cancellationToken)
    {
        var response = await http.DeleteAsync(url, cancellationToken);
        await EnsureAsync(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await EnsureAsync(response);
        var body = await response.Content.ReadFromJsonAsync<T>(JsonDefaults.Options, cancellationToken);
        return body ?? throw new ClubApiException(response.StatusCode, "The server returned an empty response.");
    }

    private static async Task EnsureAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new ClubApiException(response.StatusCode, await ApiErrors.ReadAsync(response));
    }

    private static ByteArrayContent FileContent(byte[] bytes, string contentType)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return content;
    }
}
