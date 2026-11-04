using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OBWC.Api.Data.Entities;
using OBWC.Api.Email;
using OBWC.Shared;
using OBWC.Shared.Dtos;

namespace OBWC.Tests;

public sealed class ApiTest : IDisposable
{
    public const string Password = "Beachhead1!";

    public ApiFactory Factory { get; }
    public HttpClient Client { get; }

    public ApiTest(ApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    public EmailOutbox Outbox => Factory.Services.GetRequiredService<EmailOutbox>();

    public async Task<AuthResponse> RegisterAndLoginAsync(string email, string displayName, MembershipType? membership = null)
    {
        var register = await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = Password,
            DisplayName = displayName,
            MembershipType = membership
        });
        Assert.True(register.IsSuccessStatusCode, await register.Content.ReadAsStringAsync());

        var captured = Outbox.LatestTo(email);
        Assert.NotNull(captured);
        Assert.False(string.IsNullOrWhiteSpace(captured.Link));
        var (userId, token) = ParseLink(captured.Link!);
        var confirm = await Client.PostAsJsonAsync("/api/auth/confirm-email", new ConfirmEmailRequest
        {
            UserId = userId,
            Token = token
        });
        Assert.True(confirm.IsSuccessStatusCode, await confirm.Content.ReadAsStringAsync());

        var login = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = Password
        });
        Assert.True(login.IsSuccessStatusCode, await login.Content.ReadAsStringAsync());
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth;
    }

    public HttpClient As(AuthResponse auth)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    public async Task AddRolesAsync(string email, params string[] roles)
    {
        using var scope = Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(email);
        Assert.NotNull(user);
        var result = await users.AddToRolesAsync(user, roles);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
    }

    public static (string UserId, string Token) ParseLink(string link)
    {
        var query = new Uri(link).Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        string? userId = null;
        string? token = null;
        foreach (var part in query)
        {
            var pair = part.Split('=', 2);
            var key = Uri.UnescapeDataString(pair[0]);
            var value = pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : "";
            if (key == "userId")
                userId = value;
            if (key == "token")
                token = value;
        }

        Assert.False(string.IsNullOrEmpty(userId));
        Assert.False(string.IsNullOrEmpty(token));
        return (userId!, token!);
    }

    public static CreateSessionRequest SampleEvent(string title, DateTimeOffset start) => new()
    {
        Title = title,
        ScheduledAt = start,
        EndsAt = start.AddHours(4),
        Venue = "Club table",
        Address = "Meet at the door",
        Notes = "Bring dice",
        IsPublished = true,
        AllowPlayerRsvp = true,
        Weeks = 1
    };

    public void Dispose() => Client.Dispose();
}
