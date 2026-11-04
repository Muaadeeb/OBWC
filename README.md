# Omaha Beach Wargaming Club

Club website: a standalone Blazor WebAssembly client, an ASP.NET Core Web API, and a database. The default database is a local SQLite file so a fresh clone starts with no manual database steps. SQL Server stays available for SmarterASP.NET.

The public site covers the pages from the club's page list (About, Next Event, Game Schedule, Gallery, rule sets, stores, venues, Nuke-Con, area groups, conventions, and contact). Members schedule games and read the constitution and budget. Admins maintain content and roles.

## Projects

| Project | Role |
| --- | --- |
| `src/OBWC.Client` | Blazor WebAssembly + MudBlazor. Deployed as its own site. |
| `src/OBWC.Api` | ASP.NET Core Web API, Identity, EF Core. Deployed as its own site. |
| `src/OBWC.Shared` | DTOs, roles, and Central Time formatting shared by the client and API. |
| `tests/OBWC.Tests` | xUnit API tests, including authorization, ownership, and calendar behavior. |

## Schedule

The game schedule is isolated in `src/OBWC.Api/Scheduling` and `src/OBWC.Client/Scheduling`. It follows the battletechomaha.com campaign calendar: month/list filters, create and weekly series, edit / reschedule / cancel dialogs, RSVP statuses (Going, Maybe, Not going, Waitlist when a player cap is full), check-in near the event, My Schedule, and a member `.ics` download.

Times are stored in UTC and shown as Central Time, labeled **CT**. A one-night game is not marked as a series. The Series chip appears only when more than one week was created.

BattleTech-only pieces are not included: campaigns, Contract Board, contract claims, Campaign Registry, Nightwatch, and the in-app message center. Cancel, reschedule, and waitlist notices are email, sent only when SMTP is configured.

A GM can change only events they created. Admin and SuperAdmin can change any event. Members RSVP only for themselves, and they can edit only their own "Games People Play" entries.

## Roles

- **Guest** — not signed in. Reads the public site.
- **Member** — confirmed account. Schedules games they own, RSVPs, and reads member documents.
- **GM** — add-on role for a Member, Admin, or SuperAdmin. Does not grant edit rights to someone else's events.
- **Admin** — maintains all content and assigns Member, Admin, and GM.
- **SuperAdmin** — full rights. Assigned only when the account email matches `Authorization:SuperAdminEmail`. The UI cannot grant it. Only Timothy should hold this on the live site.

## Database

`Database:Provider` is `Sqlite` or `SqlServer`.

| Provider | When | Connection |
| --- | --- | --- |
| `Sqlite` | Default. Local development and tests. | `Data Source=App_Data/obwc.db` |
| `SqlServer` | SmarterASP.NET, or any time you want SQL Server. | Set `ConnectionStrings:DefaultConnection`. Do not commit the password. |

On startup outside the test host, `App:ApplyMigrations` (default `true`) creates the database and applies EF Core migrations. In Development that is the SQLite file under `App_Data/`, which is gitignored. You do not run `dotnet ef database update` by hand.

SQLite migrations live in `src/OBWC.Api/Data/Migrations` and already include ASP.NET Identity schema version 3. The earlier SQL Server migrations, including the schema version 3 fix, are kept in `src/OBWC.Api/Data/SqlServerMigrations` and are not compiled. See that folder's README to compile them again when `Database:Provider` is `SqlServer`.

`appsettings.Production.json` selects SQL Server and a placeholder connection string. The API will not start in Production until that placeholder and `Jwt:Key` are replaced.

## Configure secrets

`appsettings.json` holds placeholders only. Do not put passwords in the repo.

Development includes a local-only JWT key (`DEV_ONLY_...`) and two SAMPLE accounts so the site runs after clone. That key is rejected outside Development. Replace it before any shared or production host:

```bash
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)" --project src/OBWC.Api
dotnet user-secrets set "Authorization:SuperAdminEmail" "the-superadmin@email" --project src/OBWC.Api
dotnet user-secrets set "Database:Provider" "SqlServer" --project src/OBWC.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=OBWC;User Id=...;Password=...;TrustServerCertificate=True" --project src/OBWC.Api
dotnet user-secrets set "Smtp:Host" "mail.example.com" --project src/OBWC.Api
dotnet user-secrets set "Smtp:UserName" "..." --project src/OBWC.Api
dotnet user-secrets set "Smtp:Password" "..." --project src/OBWC.Api
dotnet user-secrets set "Smtp:FromAddress" "club@example.com" --project src/OBWC.Api
```

The JWT key must be at least 32 characters. User-secrets override `appsettings.Development.json`, so setting `Authorization:SuperAdminEmail` to Timothy's address replaces the SAMPLE admin on your machine.

In Development, if SMTP is empty, confirmation and reset links are written to the API log and listed at `/dev/mail` on the client (the API route is `GET /api/dev/mail` and returns 404 outside Development).

Set the client API address in `src/OBWC.Client/wwwroot/appsettings.json` (`ApiBaseUrl`). That file is public. Do not put secrets in it.

## Run

```bash
dotnet run --project src/OBWC.Api
dotnet run --project src/OBWC.Client
```

API: `http://localhost:5150` (Swagger in Development at `/swagger`).
Client: `http://localhost:5163`.

Development sample accounts (not club members, password `Sample-Dev-Only-1!` from `Seed:SamplePassword`):

- `sample.member@obwc.local` — Member. Owns the SAMPLE events.
- `sample.admin@obwc.local` — Admin, and SuperAdmin only because Development sets `Authorization:SuperAdminEmail` to this address.

SAMPLE events are seeded only in Development and are titled `SAMPLE — ...`. They are not real club nights. Change `Authorization:SuperAdminEmail` to the real SuperAdmin before anyone else uses the machine.

To confirm a new account without SMTP, open **Dev mail** in the menu and use the link.

## Tests

```bash
dotnet test OBWC.slnx
```

## Seed data

Seeded in every environment, only from the club page list: the mission and dues, the 43 rule sets and the names listed with them, Rad Comics and Collectibles (794 Fort Crook Rd S, Bellevue, NE 68005), NADBAG, Nuke-Con (Bill Kuhfahl as liaison), ScroogeCon by name, and `nmh16055@gmail.com`.

Not invented: officers, venues, gallery photos, constitution text, budget text, and real events. Those pages stay empty until an admin enters them.

SAMPLE users and SAMPLE events are added only when the environment is Development.

## Deploy

See [docs/DEPLOY.md](docs/DEPLOY.md) for a manual publish to SmarterASP.NET as two sites, using SQL Server.
