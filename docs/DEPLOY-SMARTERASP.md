# Deploy OBWC to SmarterASP.NET

The live site is one IIS site plus the host's SQL Server database. `OBWC.Api` hosts the Blazor pages and the API together, the same way the BattleTech server hosts its client. SSL for omahabeachwc.com is already on the account. Nothing in this repository contains the SQL password or the JWT signing key.

| Site | Host | What you upload |
| --- | --- | --- |
| OBWC | `omahabeachwc.com` and `www.omahabeachwc.com` | The published `OBWC.Api` folder, including `web.config` and `OBWC.Api.dll` |
| Database | SmarterASP MSSQL | Schema from `deploy/sql/obwc-sqlserver.sql`, or from EF on startup |

The pages call the API on the same host. A published client leaves `ApiBaseUrl` empty, so the browser uses the site it loaded. Do not publish `OBWC.Client` on its own. That upload is only the static pages and would replace this site.

If the certificate does not already list `www`, add that name in the SmarterASP SSL panel before the first request.

## 1. Create the SQL Server database

In the SmarterASP control panel, open **MSSQL Manager** and create a database (for example `obwc`). The panel shows a connection string like:

```text
Data Source=SQL50xx.site4now.net;Initial Catalog=DB_xxxxx_obwc;User Id=DB_xxxxx_obwc_admin;Password=...;
```

Copy that string. If the API later fails with a certificate error, append `Encrypt=True;TrustServerCertificate=True`. `MultipleActiveResultSets=True` is safe to add. Do not commit the string.

The SQL login from the panel can create tables in its own database. That is what migrations need.

### Apply the schema

Pick one. They all end at the same schema. The script and `database update` are idempotent, so running a second method afterwards does not rebuild existing tables.

**A. Idempotent script.** `deploy/sql/obwc-sqlserver.sql` is the full SQL Server schema, including Identity schema version 3. Run it against the empty database with a tool that understands the `GO` batch separator (SSMS, Azure Data Studio, or `sqlcmd`). Enable remote SQL in the panel and allow your IP first. The SmarterASP web query box often rejects a script this size or a `GO` batch; use a desktop client for the script. SQL Server prints a warning that some Identity primary keys could exceed 900 bytes. That warning is expected. The script still creates the tables, and the Identity values the app stores are short enough to fit.

**B. EF from your machine.** From the repository root, with the [.NET 10 SDK](https://dotnet.microsoft.com/download) installed:

```bash
dotnet tool restore
export Database__Provider=SqlServer
export ConnectionStrings__DefaultConnection='Data Source=SQL50xx.site4now.net;Initial Catalog=DB_xxxxx_obwc;User Id=...;Password=...;TrustServerCertificate=True;Encrypt=True'
dotnet ef database update --project src/OBWC.Api
```

PowerShell:

```powershell
$env:Database__Provider = "SqlServer"
$env:ConnectionStrings__DefaultConnection = "Data Source=...;Initial Catalog=...;User Id=...;Password=...;TrustServerCertificate=True;Encrypt=True"
dotnet ef database update --project src/OBWC.Api
```

The password stays in the environment of that shell. It is not written to a file in the repo. If the tool says it is continuing without the application service provider because `Jwt:Key` is still a placeholder, the migrations still run. You do not set the JWT key in that shell.

**C. Let the API migrate on startup.** Leave `App__ApplyMigrations` at `true` (the default). The first start applies the compiled SQL Server migrations and then seeds. Do this only after the connection string is set on the server.

Startup seeding inserts the roles `Member`, `Admin`, `SuperAdmin`, and `GM`, plus the club facts (rule sets, the Rad Comics store, NADBAG, Nuke-Con, ScroogeCon, dues, and the about text) when those rows are missing. Guest is anyone who is not signed in. There is no Guest row. Sample users and sample events are inserted only when the environment is Development. SuperAdmin is not created with a password. On each start, the API grants SuperAdmin to the account whose email matches `Authorization:SuperAdminEmail`, and removes it from every other account. That person registers themselves and confirms the email.

## 2. Where the connection string goes

The API reads `ConnectionStrings:DefaultConnection`. Environment variables override JSON. Any of these work. Use one, and keep the filled-in value off git.

1. **SmarterASP environment variables** (preferred). On the API site, add:

   | Name | Value |
   | --- | --- |
   | `ASPNETCORE_ENVIRONMENT` | `Production` |
   | `ConnectionStrings__DefaultConnection` | The panel connection string |
   | `Jwt__Key` | A random string, at least 32 characters |
   | `Authorization__SuperAdminEmail` | The one SuperAdmin email. The default in `appsettings.json` is `timothyingledue@gmail.com` if you leave this unset |
   | `Smtp__Host` | Mail host |
   | `Smtp__Port` | `587` |
   | `Smtp__EnableSsl` | `true` |
   | `Smtp__UserName` | Mailbox user |
   | `Smtp__Password` | Mailbox password |
   | `Smtp__FromAddress` | From address |
   | `Smtp__FromDisplayName` | `Omaha Beach Wargaming Club` |
   | `App__PublicBaseUrl` | `https://omahabeachwc.com` |
   | `App__ClientBaseUrl` | `https://omahabeachwc.com` |
   | `App__ApplyMigrations` | `true` |
   | `App__EnableSwagger` | `false` |
   | `Cors__AllowedOrigins__0` | `https://omahabeachwc.com` |
   | `Cors__AllowedOrigins__1` | `https://www.omahabeachwc.com` |

   Production always uses the SQL Server provider, even though `appsettings.json` says `Sqlite` for local development. `Database__Provider=SqlServer` is optional.

2. **Server-side `appsettings.Production.json`.** Copy `src/OBWC.Api/appsettings.Production.example.json` to the API folder on the server, next to `OBWC.Api.dll`, and replace the placeholders. That filename is gitignored. `dotnet publish` does not copy it. Do not add the filled-in file to the repository.

3. **`web.config` on the server only.** The published file already sets `ASPNETCORE_ENVIRONMENT` to `Production` and leaves a comment where the secrets go. Add the connection string there if the panel has no environment-variable UI. Do not commit the edited file. The two `SmarterASP.pubxml` profiles are checked in and contain no password. `*.publishsettings` and `*.pubxml.user` stay gitignored.

`Jwt:Key` must be at least 32 characters. The API refuses to start while it still contains `SET_WITH` or still starts with `DEV_ONLY_`. It also refuses a SQLite file path or a `YOUR_SQL_` placeholder once it is running in Production.

The checked-in `src/OBWC.Api/web.config` is the IIS entry point. `dotnet publish` rewrites `processPath` for a self-contained build. It trusts `X-Forwarded-Proto`, which SmarterASP sends when SSL is terminated at the proxy, so HTTPS redirection does not loop. If a request redirects forever, confirm the site is passing `X-Forwarded-Proto=https`.

Uploads (gallery, venue photos, documents) go to `App_Data/uploads` under the API site, not into `wwwroot`. The application pool identity must be able to create that folder.

To see a startup failure, set `stdoutLogEnabled="true"` on the server's `web.config`, create a `logs` folder the pool can write, recycle the site, and read `logs\stdout*.log`. Set it back to `false` after.

## 3. Publish the site

Publish `OBWC.Api` to the existing website `timothyingledue-001-site3` (`omahabeachwc.com`). Set that site's .NET version to **.NET 10** (No Managed Code / Integrated) when the panel lists it. Do not create a second website for the API.

From a machine with the .NET 10 SDK, at the repository root:

```bash
dotnet publish src/OBWC.Api/OBWC.Api.csproj -c Release -o ./publish/api
```

If the panel has no .NET 10 runtime, publish self-contained instead:

```bash
dotnet publish src/OBWC.Api/OBWC.Api.csproj -c Release -r win-x64 --self-contained true -o ./publish/api
```

`appsettings.Development.json`, `appsettings.Testing.json`, and `appsettings.Production.example.json` are not in the publish folder.

**Web Deploy.** In Visual Studio, right-click `OBWC.Api` and choose **Publish**. The profile is `SmarterASP`. It deploys to `timothyingledue-001-site3` on `win1006.site4now.net` and includes the local gitignored `appsettings.Production.json`. Visual Studio asks for the control-panel password for `timothyingledue-001` and stores it in `SmarterASP.pubxml.user`, which is gitignored.

**FTP.** Upload the contents of `publish/api` (the folder that contains `web.config` and `OBWC.Api.dll`) to the site root. The FTP host and user are in the panel. Use FTP over TLS if the panel offers it.

Recycle the site after changing environment variables or `web.config`.

## 4. Pages

The Blazor pages are part of the `OBWC.Api` publish. Do not publish `OBWC.Client` to the site. The API serves `index.html` for page routes such as `/schedule`, and it serves `/api` for the data calls. A published client leaves `ApiBaseUrl` empty, so those calls stay on `omahabeachwc.com`.

Bind both `omahabeachwc.com` and `www.omahabeachwc.com` to this site.

## 5. First sign-in

Register the SuperAdmin email on the live site. The API assigns that role when the account is created, when the email is confirmed, and on each sign-in. It does not invent a password. Confirm the email before signing in. SMTP has to be set, or the confirmation link is only in the API log. Grant Admin and GM from **Users & roles**. The UI cannot grant SuperAdmin.

## 6. Checks

- `GET https://omahabeachwc.com/health` returns `{ "status": "ok" }`.
- `https://omahabeachwc.com` and `https://www.omahabeachwc.com` load the home page, and the schedule loads from that same host.
- A member can schedule a game. Another member cannot edit it. An admin can.
- Gallery upload stops at 100 photos.

## Later schema changes

Add a SQL Server migration and refresh the script. Do not hand-edit the generated statements.

```bash
export Database__Provider=SqlServer
dotnet ef migrations add Name --project src/OBWC.Api --output-dir Data/SqlServerMigrations
dotnet ef migrations script --idempotent --project src/OBWC.Api --output deploy/sql/obwc-sqlserver.sql
```

`migrations script` replaces the file. Put the `SET QUOTED_IDENTIFIER ON` batch from the current script back at the top. Filtered indexes fail when that setting is off.

SQLite migrations for local development stay in `src/OBWC.Api/Data/Migrations`. Leave `Database__Provider` unset (or set it to `Sqlite`) when adding those. Publish the API again so startup can apply the new SQL Server migration, or run `dotnet ef database update` with the connection string in the environment.
