using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OBWC.Api.Data.Seed;

var builder = WebApplication.CreateBuilder(args);
ValidateConfiguration(builder);

var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.Section));
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.Section));
builder.Services.Configure<UploadOptions>(builder.Configuration.GetSection(UploadOptions.Section));

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    DatabaseConfiguration.Configure(options, builder.Configuration, builder.Environment.ContentRootPath));

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = ClaimTypes.NameIdentifier,
        RoleClaimType = ClaimTypes.Role
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(PolicyNames.Member, policy =>
        policy.RequireRole(RoleNames.Member, RoleNames.Admin, RoleNames.SuperAdmin));
    options.AddPolicy(PolicyNames.Admin, policy =>
        policy.RequireRole(RoleNames.Admin, RoleNames.SuperAdmin));
    options.AddPolicy(PolicyNames.SuperAdmin, policy =>
        policy.RequireRole(RoleNames.SuperAdmin));
    options.AddPolicy(PolicyNames.OwnerOrAdmin, policy =>
        policy.Requirements.Add(new OwnedResourceRequirement()));
});
builder.Services.AddSingleton<IAuthorizationHandler, OwnedResourceHandler>();

builder.Services.AddSingleton<EmailOutbox>();
builder.Services.AddSingleton<SmtpEmailSender>();
builder.Services.AddSingleton<IEmailSender<ApplicationUser>>(provider => provider.GetRequiredService<SmtpEmailSender>());
builder.Services.AddSingleton<IClubMailer>(provider => provider.GetRequiredService<SmtpEmailSender>());
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<SuperAdminPromoter>();
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddSingleton<FileStorage>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Client", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()?
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
        if (origins.Length == 0)
            policy.SetIsOriginAllowed(_ => false);
        else
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Omaha Beach Wargaming Club API",
        Version = "v1"
    });
    options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste a JWT access token from /api/auth/login."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("bearer", document)] = []
    });
});

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 12 * 1024 * 1024);

var app = builder.Build();
await DbInitializer.InitializeAsync(app.Services);

app.UseForwardedHeaders();
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
    app.UseDeveloperExceptionPage();
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("OBWC");
            var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
            if (feature?.Error is not null)
                logger.LogError(feature.Error, "Unhandled exception");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Unexpected error",
                Detail = "The request could not be completed."
            });
        });
    });
    app.UseHsts();
    app.UseHttpsRedirection();
}

if (app.Configuration.GetValue("App:EnableSwagger", app.Environment.IsDevelopment()))
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "OBWC API"));
}

app.UseCors("Client");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

static void ValidateConfiguration(WebApplicationBuilder builder)
{
    if (builder.Environment.IsEnvironment("Testing"))
        return;

    var key = builder.Configuration["Jwt:Key"];
    if (string.IsNullOrWhiteSpace(key) || key.Contains("SET_WITH", StringComparison.OrdinalIgnoreCase) || key.Length < 32)
    {
        throw new InvalidOperationException(
            "Jwt:Key must be a random string of at least 32 characters. Set it with dotnet user-secrets or the Jwt__Key environment variable. Do not commit it.");
    }

    var devKey = key.StartsWith("DEV_ONLY_", StringComparison.Ordinal);
    if (devKey && !builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "Jwt:Key is the development-only key. Set a unique key with the Jwt__Key environment variable before running outside Development.");
    }

    var provider = builder.Configuration["Database:Provider"];
    var connection = builder.Configuration.GetConnectionString("DefaultConnection");
    var sqlServer = string.Equals(provider, DatabaseConfiguration.SqlServerProvider, StringComparison.OrdinalIgnoreCase);
    if (string.IsNullOrWhiteSpace(connection)
        || connection.Contains("YOUR_SQL", StringComparison.OrdinalIgnoreCase)
        || (sqlServer && connection.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)))
    {
        throw new InvalidOperationException(
            "ConnectionStrings:DefaultConnection is still a placeholder. For SQLite use a file path such as Data Source=App_Data/obwc.db. For SQL Server set Database:Provider to SqlServer and set the connection with user-secrets or ConnectionStrings__DefaultConnection.");
    }
}
