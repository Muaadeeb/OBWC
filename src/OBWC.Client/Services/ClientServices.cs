using System.Net;
using Microsoft.JSInterop;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.Authorization;
using OBWC.Shared.Dtos;

namespace OBWC.Client.Services;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    static JsonDefaults()
    {
        Options.Converters.Add(new JsonStringEnumConverter());
    }
}

public sealed class ClubApiException(HttpStatusCode status, string message) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
}

public static class ApiErrors
{
    public static async Task<string> ReadAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(text))
            return response.ReasonPhrase ?? "The request failed.";
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var messages = new List<string>();
                foreach (var property in errors.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.Array)
                        continue;
                    foreach (var item in property.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } message && !messages.Contains(message))
                            messages.Add(message);
                    }
                }

                if (messages.Count > 0)
                    return string.Join(' ', messages);
            }

            if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(detail.GetString()))
                return detail.GetString()!;
            if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(title.GetString()))
                return title.GetString()!;
        }
        catch (JsonException)
        {
        }

        return text;
    }
}

public static class FormChecks
{
    public static bool IsEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var trimmed = value.Trim();
        var at = trimmed.IndexOf('@');
        return at > 0 && at < trimmed.Length - 1 && trimmed[(at + 1)..].Contains('.') && !trimmed.Contains(' ');
    }

    public static string? PasswordProblem(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 8)
            return "Use at least 8 characters.";
        var hasUpper = value.Any(char.IsUpper);
        var hasLower = value.Any(char.IsLower);
        var hasDigit = value.Any(char.IsDigit);
        var hasSymbol = value.Any(ch => !char.IsLetterOrDigit(ch));
        if (!hasUpper || !hasLower || !hasDigit || !hasSymbol)
            return "Include an upper-case letter, a lower-case letter, a number, and a symbol.";
        return null;
    }
}

public sealed class ApiOrigin
{
    public ApiOrigin(IConfiguration configuration)
    {
        var raw = configuration["ApiBaseUrl"];
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("Set ApiBaseUrl in wwwroot/appsettings.json. It is the public URL of the API site.");
        if (!raw.EndsWith('/'))
            raw += "/";
        Base = new Uri(raw, UriKind.Absolute);
    }

    public Uri Base { get; }

    public string Absolute(string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
            return "";
        return new Uri(Base, relative.TrimStart('/')).ToString();
    }
}

public sealed class TokenStore(IJSRuntime js, ApiOrigin origin)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _access;
    private string? _refresh;
    private DateTime _expiresUtc;
    private bool _loaded;

    public async Task<string?> GetRefreshTokenAsync()
    {
        await EnsureLoadedAsync();
        return _refresh;
    }

    public async Task SaveAsync(AuthResponse auth)
    {
        _access = auth.AccessToken;
        _refresh = auth.RefreshToken;
        _expiresUtc = DateTime.SpecifyKind(auth.AccessTokenExpiresAtUtc, DateTimeKind.Utc);
        _loaded = true;
        await js.InvokeVoidAsync("obwcStorage.set", "obwc.access", _access);
        await js.InvokeVoidAsync("obwcStorage.set", "obwc.refresh", _refresh);
        await js.InvokeVoidAsync("obwcStorage.set", "obwc.expires", _expiresUtc.ToString("O"));
    }

    public async Task ClearAsync()
    {
        _access = null;
        _refresh = null;
        _expiresUtc = default;
        _loaded = true;
        await js.InvokeVoidAsync("obwcStorage.remove", "obwc.access");
        await js.InvokeVoidAsync("obwcStorage.remove", "obwc.refresh");
        await js.InvokeVoidAsync("obwcStorage.remove", "obwc.expires");
    }

    public async Task<string?> GetAccessTokenAsync(bool forceRefresh, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync();
        var stillValid = !string.IsNullOrEmpty(_access) && _expiresUtc > DateTime.UtcNow.AddSeconds(30);
        if (!forceRefresh && stillValid)
            return _access;
        if (string.IsNullOrEmpty(_refresh))
            return forceRefresh ? null : _access;
        return await RefreshAsync(cancellationToken) ? _access : null;
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loaded)
            return;
        try
        {
            _access = await js.InvokeAsync<string?>("obwcStorage.get", "obwc.access");
            _refresh = await js.InvokeAsync<string?>("obwcStorage.get", "obwc.refresh");
            var expiry = await js.InvokeAsync<string?>("obwcStorage.get", "obwc.expires");
            if (DateTime.TryParse(expiry, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
                _expiresUtc = parsed.ToUniversalTime();
        }
        catch (JSException)
        {
            _access = null;
            _refresh = null;
        }

        _loaded = true;
    }

    private async Task<bool> RefreshAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (string.IsNullOrEmpty(_refresh))
                return false;
            using var client = new HttpClient { BaseAddress = origin.Base };
            var response = await client.PostAsJsonAsync("api/auth/refresh", new RefreshRequest { RefreshToken = _refresh }, JsonDefaults.Options, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await ClearAsync();
                return false;
            }

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonDefaults.Options, cancellationToken);
            if (auth is null || string.IsNullOrEmpty(auth.AccessToken))
            {
                await ClearAsync();
                return false;
            }

            await SaveAsync(auth);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }
}

public sealed class AuthHeaderHandler(TokenStore tokens) : DelegatingHandler
{
    private static readonly HttpRequestOptionsKey<bool> Retried = new("obwc-retried");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (IsAnonymous(request))
            return await base.SendAsync(request, cancellationToken);

        await AttachAsync(request, forceRefresh: false, cancellationToken);
        if (request.Content is not null)
            await request.Content.LoadIntoBufferAsync();
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized || request.Options.TryGetValue(Retried, out _))
            return response;

        response.Dispose();
        request.Options.Set(Retried, true);
        request.Headers.Authorization = null;
        var refreshed = await AttachAsync(request, forceRefresh: true, cancellationToken);
        if (!refreshed)
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<bool> AttachAsync(HttpRequestMessage request, bool forceRefresh, CancellationToken cancellationToken)
    {
        var token = await tokens.GetAccessTokenAsync(forceRefresh, cancellationToken);
        if (string.IsNullOrEmpty(token))
            return false;
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return true;
    }

    private static bool IsAnonymous(HttpRequestMessage request)
    {
        var path = request.RequestUri?.AbsolutePath ?? "";
        return path.Contains("/api/auth/login", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/api/auth/register", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/api/auth/refresh", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/api/auth/confirm-email", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/api/auth/resend-confirmation", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/api/auth/forgot-password", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/api/auth/reset-password", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class ObwcAuthenticationStateProvider(TokenStore tokens) : AuthenticationStateProvider
{
    private ClaimsPrincipal? _cached;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_cached is not null)
            return new AuthenticationState(_cached);

        var token = await tokens.GetAccessTokenAsync(forceRefresh: false);
        _cached = string.IsNullOrEmpty(token)
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : CreatePrincipal(token) ?? new ClaimsPrincipal(new ClaimsIdentity());
        return new AuthenticationState(_cached);
    }

    public void Notify()
    {
        _cached = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public static ClaimsPrincipal? CreatePrincipal(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2)
            return null;
        try
        {
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Base64UrlDecode(parts[1])));
            var root = document.RootElement;
            var claims = new List<Claim>();
            if (root.TryGetProperty("sub", out var sub) && sub.GetString() is { Length: > 0 } id)
                claims.Add(new Claim(ClaimTypes.NameIdentifier, id));
            if (root.TryGetProperty("email", out var email))
                claims.Add(new Claim(ClaimTypes.Email, email.GetString() ?? ""));
            if (root.TryGetProperty("display_name", out var name))
                claims.Add(new Claim(ClaimTypes.Name, name.GetString() ?? ""));
            if (root.TryGetProperty("roles", out var roles) && roles.GetString() is { } roleList)
            {
                foreach (var role in roleList.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    claims.Add(new Claim(ClaimTypes.Role, role));
            }

            return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "jwt"));
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return null;
        }
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            _ => ""
        };
        return Convert.FromBase64String(padded);
    }
}

public sealed class AuthApi(HttpClient http, TokenStore tokens, ObwcAuthenticationStateProvider auth)
{
    public async Task<string?> LoginAsync(LoginRequest request)
    {
        var response = await http.PostAsJsonAsync("api/auth/login", request, JsonDefaults.Options);
        if (!response.IsSuccessStatusCode)
            return await ApiErrors.ReadAsync(response);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonDefaults.Options);
        if (body is null)
            return "Sign-in did not return a token.";
        await tokens.SaveAsync(body);
        auth.Notify();
        return null;
    }

    public async Task<string?> RegisterAsync(RegisterRequest request)
    {
        var response = await http.PostAsJsonAsync("api/auth/register", request, JsonDefaults.Options);
        return response.IsSuccessStatusCode ? null : await ApiErrors.ReadAsync(response);
    }

    public async Task<string?> ConfirmAsync(ConfirmEmailRequest request)
    {
        var response = await http.PostAsJsonAsync("api/auth/confirm-email", request, JsonDefaults.Options);
        return response.IsSuccessStatusCode ? null : await ApiErrors.ReadAsync(response);
    }

    public async Task<string?> ResendAsync(string email)
    {
        var response = await http.PostAsJsonAsync("api/auth/resend-confirmation", new ForgotPasswordRequest { Email = email }, JsonDefaults.Options);
        return response.IsSuccessStatusCode ? null : await ApiErrors.ReadAsync(response);
    }

    public async Task<string?> ForgotAsync(string email)
    {
        var response = await http.PostAsJsonAsync("api/auth/forgot-password", new ForgotPasswordRequest { Email = email }, JsonDefaults.Options);
        return response.IsSuccessStatusCode ? null : await ApiErrors.ReadAsync(response);
    }

    public async Task<string?> ResetAsync(ResetPasswordRequest request)
    {
        var response = await http.PostAsJsonAsync("api/auth/reset-password", request, JsonDefaults.Options);
        return response.IsSuccessStatusCode ? null : await ApiErrors.ReadAsync(response);
    }

    public async Task LogoutAsync()
    {
        try
        {
            var stored = await tokens.GetRefreshTokenAsync();
            if (!string.IsNullOrEmpty(stored))
                await http.PostAsJsonAsync("api/auth/logout", new RefreshRequest { RefreshToken = stored }, JsonDefaults.Options);
        }
        catch (HttpRequestException)
        {
        }

        await tokens.ClearAsync();
        auth.Notify();
    }

    public async Task<CurrentUserDto?> MeAsync()
    {
        var token = await tokens.GetAccessTokenAsync(forceRefresh: false);
        if (string.IsNullOrEmpty(token))
            return null;
        var response = await http.GetAsync("api/auth/me");
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<CurrentUserDto>(JsonDefaults.Options);
    }

    public async Task<(CurrentUserDto? User, string? Error)> UpdateProfileAsync(UpdateProfileRequest request)
    {
        var response = await http.PutAsJsonAsync("api/users/me", request, JsonDefaults.Options);
        if (!response.IsSuccessStatusCode)
            return (null, await ApiErrors.ReadAsync(response));
        return (await response.Content.ReadFromJsonAsync<CurrentUserDto>(JsonDefaults.Options), null);
    }

    public async Task<string?> ChangePasswordAsync(ChangePasswordRequest request)
    {
        var response = await http.PutAsJsonAsync("api/users/me/password", request, JsonDefaults.Options);
        return response.IsSuccessStatusCode ? null : await ApiErrors.ReadAsync(response);
    }

}
