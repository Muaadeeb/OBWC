# SQL Server migrations

These files are the EF Core migrations for **SQL Server**. They are compiled next to the SQLite migrations in `Data/Migrations`. `ProviderMigrationsAssembly` shows each provider only its own namespace, so the two histories are never applied to the same database.

The SQL Server history is:

1. `InitialCreate` — the model as first generated for SQL Server.
2. `IdentitySchemaVersion3` — widens nothing; it shortens the Identity token and login key columns to 128 characters and recreates the primary keys, which SQL Server will not alter in place. The API runs Identity schema version 3.
3. `ClubContentMaintenance` — officer contact and photo, area-group game focus and contact, convention start and end dates (drops `WhenText`), document visibility (existing rows become `MembersOnly`), and `EventPhotos`.

`dotnet ef` follows `Database__Provider`. From the repository root, after `dotnet tool restore`:

```bash
# SQLite (local default)
dotnet ef migrations add Name --project src/OBWC.Api --output-dir Data/Migrations

# SQL Server
Database__Provider=SqlServer dotnet ef migrations add Name --project src/OBWC.Api --output-dir Data/SqlServerMigrations
Database__Provider=SqlServer dotnet ef migrations script --idempotent --project src/OBWC.Api --output deploy/sql/obwc-sqlserver.sql
```

Keep the `SET QUOTED_IDENTIFIER ON` batch at the top of `deploy/sql/obwc-sqlserver.sql`. Filtered indexes fail without it. The command above replaces the file, so put that batch back after regenerating.

On Windows PowerShell, set the variable first: `$env:Database__Provider = "SqlServer"`.

Apply the script in `deploy/sql/obwc-sqlserver.sql`, or run `dotnet ef database update` with `ConnectionStrings__DefaultConnection` set in the environment. Do not put the password in the repo.
