# Archived SQL Server migrations

These files are the EF Core migrations generated for **SQL Server** (including the Identity schema version 3 fix and `ClubContentMaintenance`). They are not compiled. The app applies the SQLite migrations in `Data/Migrations` while `Database:Provider` is `Sqlite`.

`ClubContentMaintenance` matches the SQLite migration of the same name. It adds officer contact and photo columns, area-group game focus and contact, convention start and end dates (and drops the old free-text `WhenText` column), document visibility (existing rows become `MembersOnly`), and the `EventPhotos` table. Do not compile this folder and `Data/Migrations` at the same time.

To run against SQL Server again:

1. Set `Database:Provider` to `SqlServer`.
2. Set `ConnectionStrings:DefaultConnection` to the SQL Server connection string (user-secrets or environment variables — do not commit it).
3. Stop compiling the SQLite migrations and compile this folder instead. In `OBWC.Api.csproj`, remove the `Compile Remove` for `Data/SqlServerMigrations/**` and add:

```xml
<Compile Remove="Data/Migrations/**" />
```

4. The design-time factory follows `Database__Provider`. With the SQL Server migrations compiled, `dotnet ef database update --project src/OBWC.Api` applies this set.

Do not compile both folders at once. Both contain an `ApplicationDbContextModelSnapshot`.
