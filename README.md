# Omaha Beach Wargaming Club

Club website: a standalone Blazor WebAssembly client, an ASP.NET Core Web API, and a database. Local development and tests use a SQLite file so a fresh clone starts with no manual database steps. Production, including SmarterASP.NET, uses SQL Server.

The public site covers the pages from the club's page list (About, Next Event, Game Schedule, Gallery, rule sets, stores, venues, Nuke-Con, area groups, conventions, documents, and contact). Members schedule games and read member documents. A GM maintains venues and photos on games they scheduled. Admins maintain content and roles.

## Projects

| Project | Role |
| --- | --- |
| `src/OBWC.Client` | Blazor WebAssembly + MudBlazor. Served by `OBWC.Api` on the same site. |
| `src/OBWC.Api` | ASP.NET Core Web API, Identity, EF Core. Deployed as its own site. |
| `src/OBWC.Shared` | DTOs, roles, and Central Time formatting shared by the client and API. |
| `tests/OBWC.Tests` | xUnit API tests, including authorization, ownership, and calendar behavior. |

## Schedule

The game schedule is isolated in `src/OBWC.Api/Scheduling` and `src/OBWC.Client/Scheduling`. It follows the battletechomaha.com campaign calendar: month/list filters, create and weekly series, edit / reschedule / cancel dialogs, RSVP statuses (Going, Maybe, Not going, Waitlist when a player cap is full), check-in near the event, My Schedule, and a member `.ics` download.

Times are stored in UTC and shown as Central Time, labeled **CT**. A one-night game is not marked as a series. The Series chip appears only when more than one week was created.

BattleTech-only pieces are not included: campaigns, Contract Board, contract claims, Campaign Registry, Nightwatch, and the in-app message center. Cancel, reschedule, and waitlist notices are email, sent only when SMTP is configured.

A GM can change only events they created, and can upload photos only on those events. A GM can edit or delete only photos they uploaded. Admin and SuperAdmin can change any event and any event photo, and they manage the global gallery. Members RSVP only for themselves, and they can edit only their own "Games People Play" entries. Convention dates are calendar dates, not clock times.

## Roles

- **Guest** — not signed in. Reads the public site.
- **Member** — confirmed account. Schedules games they own, RSVPs, and reads member documents.
- **GM** — add-on role for a Member, Admin, or SuperAdmin. Does not grant edit rights to someone else's events or photos. A GM can create and edit venues.
- **Admin** — maintains officers, the gallery, conventions, area groups, documents, venues, and every event and event photo. Assigns Member, Admin, and GM.
- **SuperAdmin** — full rights. Assigned only when the account email matches `Authorization:SuperAdminEmail`. The UI cannot grant it. The default in `appsettings.json` is `timothyingledue@gmail.com`. No password for that address is stored; Timothy registers the account himself.

## Database

`Database:Provider` is `Sqlite` or `SqlServer`. Production ignores that setting and always uses SQL Server.

| Provider | When | Connection |
| --- | --- | --- |
| `Sqlite` | Automated tests, and the checked-in default in `appsettings.json`. | `Data Source=App_Data/obwc.db` |
| `SqlServer` | Local development and production, including SmarterASP.NET. | `ConnectionStrings:DefaultConnection` from the gitignored `appsettings.Production.json`. |

Local Development copies only that connection string out of `appsettings.Production.json`. It does not copy the production JWT key, SMTP password, or site URLs. On startup outside the test host, `App:ApplyMigrations` (default `true`) applies the migrations for the active provider. A Development start therefore applies pending SQL Server migrations to the live database. Sample accounts and SAMPLE events are not seeded onto that database.

SQLite migrations live in `src/OBWC.Api/Data/Migrations`. SQL Server migrations live in `src/OBWC.Api/Data/SqlServerMigrations`. Both are compiled. The API applies only the set that matches the provider. An idempotent script for the SQL Server set is in `deploy/sql/obwc-sqlserver.sql`.

`appsettings.Production.example.json` shows the production shape with placeholders. The real `appsettings.Production.json` is gitignored. The API will not start in Production while the connection string or `Jwt:Key` is still a placeholder, and it rejects the development-only JWT key outside Development.

## Configure secrets

`appsettings.json` holds the default SuperAdmin email and otherwise placeholders. Do not put passwords in the repo. `Authorization:SuperAdminEmail` defaults to `timothyingledue@gmail.com`. That is not a secret and it does not create an account.

Development includes a local-only JWT key (`DEV_ONLY_...`) and two SAMPLE accounts so the site runs after clone. That key is rejected outside Development. Replace it before any shared or production host:

```bash
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)" --project src/OBWC.Api
dotnet user-secrets set "Authorization:SuperAdminEmail" "timothyingledue@gmail.com" --project src/OBWC.Api
dotnet user-secrets set "Database:Provider" "SqlServer" --project src/OBWC.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=OBWC;User Id=...;Password=...;TrustServerCertificate=True" --project src/OBWC.Api
dotnet user-secrets set "Smtp:Host" "mail.example.com" --project src/OBWC.Api
dotnet user-secrets set "Smtp:UserName" "..." --project src/OBWC.Api
dotnet user-secrets set "Smtp:Password" "..." --project src/OBWC.Api
dotnet user-secrets set "Smtp:FromAddress" "club@example.com" --project src/OBWC.Api
```

The JWT key must be at least 32 characters. Development keeps the local JWT key and does not override `Authorization:SuperAdminEmail`, so a local start against the live database leaves SuperAdmin on `timothyingledue@gmail.com`. User-secrets override the settings files. `dotnet publish` leaves `appsettings.Development.json`, `appsettings.Testing.json`, and `appsettings.Production.example.json` out of the output, and it does not copy `appsettings.Production.json`. A published site uses `timothyingledue@gmail.com` unless `Authorization__SuperAdminEmail` replaces it. On each sign-in the API keeps SuperAdmin only on the configured email and removes it from any other account.

In Development, if SMTP is empty, confirmation and reset links are written to the API log and listed at `/dev/mail` on the client (the API route is `GET /api/dev/mail` and returns 404 outside Development).

The API project hosts the Blazor pages, the same way the BattleTech server hosts its client. One site serves both. `wwwroot/appsettings.json` still points local split-debug at `http://localhost:5150`. A published site leaves `ApiBaseUrl` empty, so the pages call whatever host they were loaded from. Both files are public. Do not put secrets in them.

## Run

```bash
dotnet run --project src/OBWC.Api
```

Site: `http://localhost:5150` (Swagger in Development at `/swagger`).

Sign in with a real account on the live database. The sample accounts (`sample.member@obwc.local`, `sample.admin@obwc.local`, `sample.guest@obwc.local`) are not created when Development is using SQL Server.

To confirm a new account without SMTP, open **Dev mail** in the menu and use the link.

## Tests

```bash
dotnet test OBWC.slnx
```

## Seed data

Seeded in every environment, only from the club page list: the mission and dues, the 43 rule sets and the names listed with them, Rad Comics and Collectibles (794 Fort Crook Rd S, Bellevue, NE 68005), NADBAG (name and the existing "An ASL group." description; no invented game focus, URL, or contact), Nuke-Con (Bill Kuhfahl as liaison), ScroogeCon by name, and `nmh16055@gmail.com`.

Not invented: officers, venues, gallery photos, constitution text, budget text, minutes, and real events. Those pages stay empty until an admin enters them.

## Maintained content

Server policies enforce these rules. Hiding a button is not the check.

| Page | Who maintains it | Public page |
| --- | --- | --- |
| Officers (`/admin/officers`) | Admin. Name, title, term, optional photo and contact, display order. | About reads the officer list from the database. |
| Venues (`/admin/venues`) | GM and Admin. Name, address, notes, inside and outside photos. | `/venues` |
| Gallery (`/admin/gallery`) | Admin. Global photos of public events, with a size and type limit. | `/gallery` |
| Game photos | GM on games they scheduled, for photos they uploaded. Admin on every game and every photo. | Shown on the game in the schedule. Times are CT. |
| Conventions (`/admin/conventions`) | Admin. Name, start and end dates, location, URL, description. | `/conventions`. Blank date, location, URL, and description lines are omitted. |
| Area groups (`/admin/groups`) | Admin. Name, game focus, URL, contact, description. | `/groups`. Blank fields are omitted. |
| Documents (`/admin/documents`) | Admin. PDF only: constitution, budget, or minutes. Rename and delete. Visibility is Public or Members only, and new files default to Members only. | `/documents` lists what the caller may see. Member pages list the same files for one category. |

Uploads go to `Uploads:Path` (default `App_Data/uploads`), a path relative to the API content root or an absolute path. The API checks the extension, the file signature, and `Uploads:MaxUploadBytes` (default 10 MB). Nothing secret is written into the file name. The gallery also stops at `Uploads:MaxGalleryPhotos` (default 100).

SAMPLE users and SAMPLE events are added only for a Development run that is still on SQLite. A Development run on SQL Server does not add them.

## Deploy

See [docs/DEPLOY-SMARTERASP.md](docs/DEPLOY-SMARTERASP.md) for the SmarterASP.NET publish: SQL Server schema, the connection string, and the API and Blazor client sites for omahabeachwc.com.
