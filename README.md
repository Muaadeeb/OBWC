# Omaha Beach Wargaming Club

Phase 1 of the club website: a standalone Blazor WebAssembly client, an ASP.NET Core Web API, and a SQL Server database.

The public site covers the pages from the club's page list (About, Next Event, Game Schedule, Gallery, rule sets, stores, venues, Nuke-Con, area groups, conventions, and contact). Members schedule games and read the constitution and budget. Admins maintain content and roles.

## Projects

| Project | Role |
| --- | --- |
| `src/OBWC.Client` | Blazor WebAssembly + MudBlazor. Deployed as its own site. |
| `src/OBWC.Api` | ASP.NET Core Web API, Identity, EF Core. Deployed as its own site. |
| `src/OBWC.Shared` | DTOs, roles, and validation shared by the client and API. |
| `tests/OBWC.Tests` | xUnit API tests, including authorization and calendar behavior. |

## Schedule

The game schedule is isolated in `src/OBWC.Api/Scheduling` and `src/OBWC.Client/Scheduling`. It follows the battletechomaha.com campaign calendar: month/list filters, create and weekly series, edit / reschedule / cancel dialogs, RSVP statuses (Going, Maybe, Not going, Waitlist when a player cap is full), check-in near the event, My Schedule, and a member `.ics` download.

BattleTech-only pieces are not included: campaigns, Contract Board, contract claims, Campaign Registry, Nightwatch, and the in-app message center. Cancel, reschedule, and waitlist notices are email, sent only when SMTP is configured.

A GM can change only events they created. Admin and SuperAdmin can change any event. Members RSVP only for themselves.

## Roles

- **Guest** — not signed in. Reads the public site.
- **Member** — confirmed account. Schedules games they own, RSVPs, and reads member documents.
- **GM** — add-on role. Does not grant edit rights to someone else's events.
- **Admin** — maintains all content and assigns Member, Admin, and GM.
- **SuperAdmin** — full rights. Assigned only when the account email matches `Authorization:SuperAdminEmail`. The UI cannot grant it.

## Configure secrets

`appsettings.json` holds placeholders only. Do not put passwords in the repo.

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=OBWC;User Id=...;Password=...;TrustServerCertificate=True" --project src/OBWC.Api
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)" --project src/OBWC.Api
dotnet user-secrets set "Authorization:SuperAdminEmail" "the-superadmin@email" --project src/OBWC.Api
dotnet user-secrets set "Smtp:Host" "mail.example.com" --project src/OBWC.Api
dotnet user-secrets set "Smtp:UserName" "..." --project src/OBWC.Api
dotnet user-secrets set "Smtp:Password" "..." --project src/OBWC.Api
dotnet user-secrets set "Smtp:FromAddress" "club@example.com" --project src/OBWC.Api
```

The JWT key must be at least 32 characters. The API refuses to start while the connection string or JWT key is still the placeholder. In Development, if SMTP is empty, confirmation and reset links are written to the API log.

Set the client API address in `src/OBWC.Client/wwwroot/appsettings.json` (`ApiBaseUrl`). That file is public. Do not put secrets in it.

## Run

```bash
dotnet run --project src/OBWC.Api
dotnet run --project src/OBWC.Client
```

API: `http://localhost:5150` (Swagger in Development at `/swagger`).
Client: `http://localhost:5163`.

Register the SuperAdmin email, confirm it, and sign in. That account receives SuperAdmin. Use it to grant Admin and GM.

## Tests

```bash
dotnet test OBWC.slnx
```

## Seed data

Seeded only from the club page list: the mission and dues, the 43 rule sets and the names listed with them, Rad Comics and Collectibles (794 Fort Crook Rd S, Bellevue, NE 68005), NADBAG, Nuke-Con (Bill Kuhfahl as liaison), ScroogeCon by name, and `nmh16055@gmail.com`. No events, venues, officers, or accounts are invented.

## Deploy

See [docs/DEPLOY.md](docs/DEPLOY.md) for a manual publish to SmarterASP.NET as two sites.
