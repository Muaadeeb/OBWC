using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace OBWC.Api.Identity;

public sealed class TokenService(
    ApplicationDbContext db,
    UserManager<ApplicationUser> users,
    IOptions<JwtOptions> jwtOptions)
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<AuthResponse> IssueAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var roles = await users.GetRolesAsync(user);
        var expires = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes <= 0 ? 30 : _jwt.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? ""),
            new(ClaimTypes.Name, user.DisplayName),
            new("display_name", user.DisplayName),
            new("roles", string.Join(' ', roles.OrderBy(role => role, StringComparer.Ordinal)))
        };
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: credentials);

        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = Hash(refresh),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays <= 0 ? 14 : _jwt.RefreshTokenDays),
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);

        return new AuthResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            RefreshToken = refresh,
            AccessTokenExpiresAtUtc = expires,
            User = ToCurrentUser(user, roles)
        };
    }

    public async Task<AuthResponse?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = Hash(refreshToken);
        var existing = await db.RefreshTokens
            .Include(token => token.User)
            .SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);
        if (existing?.User is null)
            return null;

        if (existing.RevokedAtUtc is not null || existing.ExpiresAtUtc <= DateTime.UtcNow)
        {
            if (existing.RevokedAtUtc is not null)
                await RevokeActiveTokensAsync(existing.UserId, cancellationToken);
            return null;
        }

        existing.RevokedAtUtc = DateTime.UtcNow;
        var issued = await IssueAsync(existing.User, cancellationToken);
        existing.ReplacedByHash = Hash(issued.RefreshToken);
        await db.SaveChangesAsync(cancellationToken);
        return issued;
    }

    public async Task RevokeAsync(string userId, string refreshToken, CancellationToken cancellationToken)
    {
        var hash = Hash(refreshToken);
        var existing = await db.RefreshTokens
            .SingleOrDefaultAsync(token => token.TokenHash == hash && token.UserId == userId, cancellationToken);
        if (existing is null || existing.RevokedAtUtc is not null)
            return;
        existing.RevokedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static CurrentUserDto ToCurrentUser(ApplicationUser user, IEnumerable<string> roles) => new()
    {
        Id = user.Id,
        Email = user.Email ?? "",
        DisplayName = user.DisplayName,
        MembershipType = user.MembershipType,
        EmailConfirmed = user.EmailConfirmed,
        Roles = roles.OrderBy(role => role, StringComparer.Ordinal).ToArray()
    };

    private async Task RevokeActiveTokensAsync(string userId, CancellationToken cancellationToken)
    {
        var active = await db.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var token in active)
            token.RevokedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
    }
}
