# Deploy to SmarterASP.NET

Publish the API and the Blazor client as **two sites**. The API talks to SQL Server on the host. The client is static WebAssembly files that call the API. CORS must allow the client's public origin.

The host needs the ASP.NET Core 10 hosting bundle for a framework-dependent API. If the panel does not offer .NET 10, publish the API self-contained for `win-x64` (the command is below).

## 1. Database

Create a SQL Server database in the SmarterASP panel. The live site should use SQL Server, not the SQLite file used for local development.

Set `Database__Provider` to `SqlServer`. Copy the host, database name, user, and password into `ConnectionStrings__DefaultConnection`. Do not commit that string.

The migrations compiled into the API are the SQLite set. Before publishing for SQL Server, compile the archived SQL Server migrations instead. In `src/OBWC.Api/OBWC.Api.csproj`, remove the `Compile Remove` for `Data/SqlServerMigrations/**` and exclude `Data/Migrations/**`. Details are in `src/OBWC.Api/Data/SqlServerMigrations/README.md`. Those archived migrations include the Identity schema version 3 fix. Then publish. With `App__ApplyMigrations=true`, startup applies that SQL Server set.

Leaving the provider on `Sqlite` would create `App_Data/obwc.db` on the server. That is only a fallback. Use SQL Server on SmarterASP.NET.

## 2. Two sites

Create one site for the API and one site for the client. Give them separate application pools. Point each site's document root at its own folder.

## 3. API configuration

Set these on the API site (environment variables or the published `web.config` `<environmentVariables>`). Replace the placeholders. Do not commit the filled-in file.

| Name | Value |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Database__Provider` | `SqlServer` |
| `ConnectionStrings__DefaultConnection` | SQL connection string from the panel |
| `Jwt__Key` | Random string, at least 32 characters |
| `Jwt__Issuer` | `OBWC` |
| `Jwt__Audience` | `OBWC.Client` |
| `Authorization__SuperAdminEmail` | The one SuperAdmin email |
| `Smtp__Host` | Mail host |
| `Smtp__Port` | `587` (or `465`) |
| `Smtp__EnableSsl` | `true` |
| `Smtp__UserName` | Mailbox user |
| `Smtp__Password` | Mailbox password |
| `Smtp__FromAddress` | From address |
| `Smtp__FromDisplayName` | `Omaha Beach Wargaming Club` |
| `App__PublicBaseUrl` | Public API URL, no trailing path |
| `App__ClientBaseUrl` | Public client URL, used in email links |
| `App__ApplyMigrations` | `true` so startup applies EF migrations |
| `App__EnableSwagger` | `false` unless you want `/swagger` on the public API |
| `Cors__AllowedOrigins__0` | The client site origin, for example `https://www.example.com` |

Example `web.config` fragment (placeholders only):

```xml
<aspNetCore processPath="dotnet" arguments=".\OBWC.Api.dll" stdoutLogEnabled="false">
  <environmentVariables>
    <environmentVariable name="Database__Provider" value="SqlServer" />
    <environmentVariable name="ConnectionStrings__DefaultConnection" value="Server=YOUR_SQL_HOST;Database=OBWC;User Id=YOUR_SQL_USER;Password=YOUR_SQL_PASSWORD;TrustServerCertificate=True;Encrypt=True" />
    <environmentVariable name="Jwt__Key" value="REPLACE_WITH_A_LONG_RANDOM_KEY" />
    <environmentVariable name="Authorization__SuperAdminEmail" value="the-superadmin@email" />
    <environmentVariable name="App__ClientBaseUrl" value="https://CLIENT_HOST" />
    <environmentVariable name="App__PublicBaseUrl" value="https://API_HOST" />
    <environmentVariable name="Cors__AllowedOrigins__0" value="https://CLIENT_HOST" />
  </environmentVariables>
</aspNetCore>
```

The API trusts `X-Forwarded-Proto` so HTTPS redirection works behind the host's proxy.

Uploads (gallery, venue photos, constitution, budget) are stored under `App_Data/uploads` on the API site, not in `wwwroot`. The application pool identity must be able to create that folder. Member documents are downloaded through the API, not as public files.

On startup, with `App__ApplyMigrations=true`, the API applies EF Core migrations and seeds club facts if those rows are missing.

## 4. Publish

Publish only after the SQL Server migrations are the ones compiled into the API (section 1). `appsettings.Development.json` and `appsettings.Testing.json` are not copied into the publish folder. Production uses `appsettings.Production.json` (SQL Server and a placeholder connection string) plus the environment variables above. The API will not start while the connection string or `Jwt:Key` is still a placeholder, and it rejects the development-only JWT key outside Development.

From the repository root, on a machine with the .NET 10 SDK:

```bash
dotnet publish src/OBWC.Api/OBWC.Api.csproj -c Release -o ./publish/api
dotnet publish src/OBWC.Client/OBWC.Client.csproj -c Release -o ./publish/client
```

Self-contained API if the host has no .NET 10 runtime:

```bash
dotnet publish src/OBWC.Api/OBWC.Api.csproj -c Release -r win-x64 --self-contained true -o ./publish/api
```

Before publishing the client, set `ApiBaseUrl` in `src/OBWC.Client/wwwroot/appsettings.json` to the public API origin (for example `https://api.example.com`). That file is downloaded by the browser. It must not contain secrets. You can also edit `appsettings.json` in the published client folder and recycle the site.

Upload the API publish output to the API site. Upload the **contents of** `publish/client/wwwroot` (including `web.config`) to the client site. `web.config` maps `.wasm` and sends unknown paths to `index.html` so client routes work on IIS.

## 5. First sign-in

Register the SuperAdmin email on the live site and confirm it (SMTP must work, or confirm through the log if you temporarily enable logging). That account becomes SuperAdmin. Grant Admin and GM from **Users & roles**. SuperAdmin cannot be granted from that screen.

## 6. Checks

- `GET https://API_HOST/health` returns `{ "status": "ok" }`.
- The client home page loads and the schedule calls the API without a browser CORS error.
- A member can schedule a game. Another member cannot edit it. An admin can.
- Gallery upload stops at 100 photos.
