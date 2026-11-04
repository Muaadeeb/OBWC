namespace OBWC.Api.Options;

public class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "OBWC";
    public string Audience { get; set; } = "OBWC.Client";
    public string Key { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 14;
}

public class SmtpOptions
{
    public const string Section = "Smtp";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromDisplayName { get; set; } = "Omaha Beach Wargaming Club";
}

public class AppOptions
{
    public const string Section = "App";
    public string PublicBaseUrl { get; set; } = "";
    public string ClientBaseUrl { get; set; } = "http://localhost:5163";
    public string ContactEmail { get; set; } = ClubContentContact.Email;
    public bool ApplyMigrations { get; set; } = true;
    public bool EnableSwagger { get; set; }
}

public static class ClubContentContact
{
    public const string Email = "nmh16055@gmail.com";
}

public class UploadOptions
{
    public const string Section = "Uploads";
    public string Path { get; set; } = "App_Data/uploads";
    public int MaxGalleryPhotos { get; set; } = GalleryLimits.MaxPhotos;
    public long MaxUploadBytes { get; set; } = 10 * 1024 * 1024;
}
