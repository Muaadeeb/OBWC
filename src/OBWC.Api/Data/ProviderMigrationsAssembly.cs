using System.Reflection;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace OBWC.Api.Data;

/// <summary>
/// EF Core discovers every migration in the assembly. SQLite and SQL Server each have a
/// full migration history, so the active provider must see only its own namespace.
/// </summary>
public sealed class ProviderMigrationsAssembly : IMigrationsAssembly
{
    private readonly Type _contextType;
    private readonly IMigrationsIdGenerator _idGenerator;
    private readonly string _migrationsNamespace;
    private IReadOnlyDictionary<string, TypeInfo>? _migrations;
    private ModelSnapshot? _modelSnapshot;
    private bool _snapshotResolved;

    public ProviderMigrationsAssembly(
        ICurrentDbContext currentContext,
        IDbContextOptions options,
        IMigrationsIdGenerator idGenerator)
    {
        _contextType = currentContext.Context.GetType();
        _idGenerator = idGenerator;
        _migrationsNamespace = options.Extensions.Any(extension => extension.GetType().Name == "SqlServerOptionsExtension")
            ? DatabaseConfiguration.SqlServerMigrationsNamespace
            : DatabaseConfiguration.SqliteMigrationsNamespace;

        var relational = options.Extensions.OfType<RelationalOptionsExtension>().FirstOrDefault();
        Assembly = relational?.MigrationsAssemblyObject
            ?? (relational?.MigrationsAssembly is { } assemblyName
                ? System.Reflection.Assembly.Load(new AssemblyName(assemblyName))
                : _contextType.Assembly);
    }

    public Assembly Assembly { get; }

    public IReadOnlyDictionary<string, TypeInfo> Migrations
        => _migrations ??= LoadMigrations();

    public ModelSnapshot? ModelSnapshot
    {
        get
        {
            if (_snapshotResolved)
                return _modelSnapshot;

            var snapshots = CandidateTypes()
                .Where(type => type.IsSubclassOf(typeof(ModelSnapshot)) && BelongsToContext(type))
                .ToList();
            if (snapshots.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Expected one EF model snapshot in {_migrationsNamespace}, found {snapshots.Count}.");
            }

            _modelSnapshot = snapshots.Count == 0
                ? null
                : (ModelSnapshot)Activator.CreateInstance(snapshots[0])!;
            _snapshotResolved = true;
            return _modelSnapshot;
        }
    }

    public string? FindMigrationId(string nameOrId)
        => Migrations.Keys.FirstOrDefault(id =>
            _idGenerator.IsValidId(nameOrId)
                ? string.Equals(id, nameOrId, StringComparison.OrdinalIgnoreCase)
                : string.Equals(_idGenerator.GetName(id), nameOrId, StringComparison.OrdinalIgnoreCase));

    public Migration CreateMigration(TypeInfo migrationClass, string activeProvider)
    {
        var migration = (Migration)Activator.CreateInstance(migrationClass.AsType())!;
        migration.ActiveProvider = activeProvider;
        return migration;
    }

    private IReadOnlyDictionary<string, TypeInfo> LoadMigrations()
    {
        var result = new SortedList<string, TypeInfo>(StringComparer.Ordinal);
        foreach (var type in CandidateTypes().Where(type => type.IsSubclassOf(typeof(Migration)) && BelongsToContext(type)))
        {
            var id = type.GetCustomAttribute<MigrationAttribute>()?.Id;
            if (id is null)
                continue;
            result.Add(id, type.GetTypeInfo());
        }

        return result;
    }

    private IEnumerable<Type> CandidateTypes()
        => Assembly.GetTypes().Where(type =>
            type is { IsClass: true, IsAbstract: false }
            && (type.Namespace == _migrationsNamespace
                || (type.Namespace?.StartsWith(_migrationsNamespace + ".", StringComparison.Ordinal) ?? false)));

    private bool BelongsToContext(Type type)
    {
        var current = type;
        while (current is not null && current != typeof(object))
        {
            var attribute = current.GetCustomAttribute<DbContextAttribute>(inherit: false);
            if (attribute is not null)
                return attribute.ContextType == _contextType;
            current = current.BaseType;
        }

        return false;
    }
}
