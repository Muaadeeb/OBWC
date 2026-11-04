using System.Net;
using System.Net.Http.Json;
using OBWC.Shared;
using OBWC.Shared.Dtos;

namespace OBWC.Tests;

public class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>, IDisposable
{
    private readonly ApiTest _api = new(factory);

    [Fact]
    public async Task Login_is_rejected_until_email_is_confirmed()
    {
        var email = "pending@obwc.test";
        var register = await _api.Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = ApiTest.Password,
            DisplayName = "Pending Member"
        });
        Assert.Equal(HttpStatusCode.Accepted, register.StatusCode);

        var login = await _api.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = ApiTest.Password
        });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        Assert.Contains("Confirm your email", await login.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refresh_rotates_tokens_and_password_reset_works()
    {
        var email = "reset@obwc.test";
        var auth = await _api.RegisterAndLoginAsync(email, "Reset Member");
        using var authed = _api.As(auth);
        var me = await authed.GetFromJsonAsync<CurrentUserDto>("/api/auth/me");
        Assert.Equal("Reset Member", me!.DisplayName);
        Assert.Contains(RoleNames.Member, me.Roles);
        Assert.DoesNotContain(RoleNames.SuperAdmin, me.Roles);

        var refresh = await _api.Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest { RefreshToken = auth.RefreshToken });
        Assert.True(refresh.IsSuccessStatusCode, await refresh.Content.ReadAsStringAsync());
        var rotated = await refresh.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(rotated);
        Assert.NotEqual(auth.RefreshToken, rotated.RefreshToken);

        var reused = await _api.Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest { RefreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);

        using var stillAuthed = _api.As(rotated);
        var meAgain = await stillAuthed.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meAgain.StatusCode);

        var forgot = await _api.Client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest { Email = email });
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        var mail = _api.Outbox.LatestTo(email);
        Assert.NotNull(mail?.Link);
        Assert.Contains("/account/reset-password", mail.Link, StringComparison.Ordinal);
        var (userId, token) = ApiTest.ParseLink(mail.Link);
        var reset = await _api.Client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest
        {
            UserId = userId,
            Token = token,
            NewPassword = "NewBeach1!"
        });
        Assert.True(reset.IsSuccessStatusCode, await reset.Content.ReadAsStringAsync());

        var oldPassword = await _api.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = ApiTest.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        var newPassword = await _api.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = "NewBeach1!" });
        Assert.Equal(HttpStatusCode.OK, newPassword.StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_comes_from_config_and_cannot_be_granted_in_the_api()
    {
        var owner = await _api.RegisterAndLoginAsync("owner@obwc.test", "Configured Owner");
        Assert.Contains(RoleNames.SuperAdmin, owner.User.Roles);
        Assert.Contains(RoleNames.Member, owner.User.Roles);

        await _api.RegisterAndLoginAsync("admin@obwc.test", "Club Admin");
        await _api.AddRolesAsync("admin@obwc.test", RoleNames.Admin);
        var adminLogin = await _api.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "admin@obwc.test",
            Password = ApiTest.Password
        });
        var admin = await adminLogin.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(admin);

        var member = await _api.RegisterAndLoginAsync("player@obwc.test", "Club Player");
        using var adminClient = _api.As(admin);
        var grant = await adminClient.PutAsJsonAsync($"/api/users/{member.User.Id}/roles", new UpdateRolesRequest
        {
            Member = true,
            SuperAdmin = true
        });
        Assert.Equal(HttpStatusCode.BadRequest, grant.StatusCode);
        Assert.Contains("cannot be granted", await grant.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var memberClient = _api.As(await LoginAsync("player@obwc.test"));
        var memberMe = await memberClient.GetFromJsonAsync<CurrentUserDto>("/api/auth/me");
        Assert.DoesNotContain(RoleNames.SuperAdmin, memberMe!.Roles);

        var tamper = await adminClient.PutAsJsonAsync($"/api/users/{owner.User.Id}/roles", new UpdateRolesRequest
        {
            Member = false,
            Admin = false,
            Gm = false
        });
        Assert.Equal(HttpStatusCode.Forbidden, tamper.StatusCode);

        var ownerStill = await LoginAsync("owner@obwc.test");
        Assert.Contains(RoleNames.SuperAdmin, ownerStill.User.Roles);

        using var ownerClient = _api.As(ownerStill);
        var users = await ownerClient.GetFromJsonAsync<List<UserSummaryDto>>("/api/users");
        Assert.Contains(users!, user => user.Email == "owner@obwc.test" && user.Roles.Contains(RoleNames.SuperAdmin));

        using var plain = _api.As(member);
        var forbidden = await plain.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var anonymous = await _api.Client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
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
