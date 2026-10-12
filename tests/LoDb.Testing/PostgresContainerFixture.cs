using System.Globalization;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace LoDb.Testing;

/// <summary>
/// A throwaway PostgreSQL server, shared by the tests of an assembly.
/// </summary>
/// <remarks>
/// Same major version and flavour as the production image, without durability: a test server
/// never survives a crash, and skipping the flushes makes creating and writing a database
/// several times faster. The container is removed when the fixture is disposed, and by the
/// Testcontainers reaper if the run dies first. Tests that write share the server, never a
/// database: each creates its own. The server's <see cref="ConnectionString"/> is its
/// superuser's, as <c>migrate</c> connects; the API connects as the role of
/// <see cref="TestRoles"/> (<see cref="TestDatabase.AppConnectionString"/>).
/// </remarks>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    public const string Image = "postgres:17-alpine";

    private const string TemplateName = "lodb_template";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image)
        .WithCommand("-c", "fsync=off")
        .WithCommand("-c", "synchronous_commit=off")
        .WithCommand("-c", "full_page_writes=off")
        .Build();

    private readonly SemaphoreSlim _templateGate = new(1, 1);
    private volatile bool _isTemplateReady;

    /// <summary>Connection string of the server, for <c>ConnectionStrings:LoDb</c>.</summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>A new empty database, dropped when the returned value is disposed.</summary>
    public Task<TestDatabase> CreateDatabaseAsync(CancellationToken cancellationToken) =>
        CreateDatabaseAsync(template: null, cancellationToken);

    /// <summary>
    /// A new database at the latest migration, as <c>migrate</c> leaves it; dropped when the
    /// returned value is disposed.
    /// </summary>
    /// <remarks>
    /// A copy of a template migrated once per server, migrated again: <c>CREATE DATABASE</c>
    /// copies the schema and its grants, not the grants on the database itself nor the API
    /// role's settings in it, which the second run of <c>migrate</c> puts back with nothing
    /// left to apply.
    /// </remarks>
    public async Task<TestDatabase> CreateMigratedDatabaseAsync(
        CancellationToken cancellationToken)
    {
        await EnsureTemplateAsync(cancellationToken);
        var database = await CreateDatabaseAsync(TemplateName, cancellationToken);
        await database.MigrateAsync(cancellationToken);
        return database;
    }

    /// <summary>
    /// <c>pg_dump --schema-only</c> of a database, without the given tables, normalized as
    /// <c>tools/schema/check.sh</c> does.
    /// </summary>
    public async Task<IReadOnlyList<string>> DumpSchemaAsync(
        string database,
        IEnumerable<string> excludedTables,
        CancellationToken cancellationToken)
    {
        var username = new NpgsqlConnectionStringBuilder(ConnectionString).Username!;
        string[] command =
        [
            "pg_dump",
            "--username", username,
            "--schema-only",
            "--no-owner",
            "--no-privileges",
            .. excludedTables.Select(static table => $"--exclude-table=public.\"{table}\""),
            database,
        ];
        var result = await _container.ExecAsync(command, cancellationToken);
        return result.ExitCode == 0
            ? SchemaDump.Normalize(result.Stdout)
            : throw new InvalidOperationException($"pg_dump failed: {result.Stderr}");
    }

    /// <summary>
    /// <c>pg_dump --format=custom</c> of a whole database as <paramref name="role"/>, the way
    /// the host's backups dump it, through the server's own socket: the size of the dump.
    /// </summary>
    public async Task<long> DumpAsAsync(
        string database,
        string role,
        CancellationToken cancellationToken)
    {
        var file = $"/tmp/{Guid.NewGuid():N}.dump";
        string[] command =
        [
            "sh", "-c",
            "pg_dump --username=\"$1\" --format=custom --compress=0 --file=\"$3\" \"$2\""
                + " && wc -c < \"$3\" && rm -f \"$3\"",
            "dump", role, database, file,
        ];
        var result = await _container.ExecAsync(command, cancellationToken);
        return result.ExitCode == 0
            ? long.Parse(result.Stdout.Trim(), CultureInfo.InvariantCulture)
            : throw new InvalidOperationException($"pg_dump failed: {result.Stderr}");
    }

    /// <summary>
    /// Starts the server, then puts the roles of <see cref="TestRoles"/> in place once, before
    /// any test migrates a database of its own.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _container.StartAsync(cancellationToken);
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await TestRoles.Create().EnsureRolesAsync(connection, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _templateGate.Dispose();
        await _container.DisposeAsync();
    }

    private async Task<TestDatabase> CreateDatabaseAsync(
        string? template,
        CancellationToken cancellationToken)
    {
        var name = "test_" + Guid.NewGuid().ToString("N");
        var copy = template is null ? string.Empty : $" TEMPLATE {template}";
        await ExecuteOnServerAsync($"CREATE DATABASE {name}{copy}", cancellationToken);
        return new TestDatabase(this, name, DatabaseConnectionString(name));
    }

    // Once migrated, the template refuses connections: a copy fails while anyone is connected.
    private async Task EnsureTemplateAsync(CancellationToken cancellationToken)
    {
        if (_isTemplateReady)
        {
            return;
        }

        await _templateGate.WaitAsync(cancellationToken);
        try
        {
            if (_isTemplateReady)
            {
                return;
            }

            await ExecuteOnServerAsync($"CREATE DATABASE {TemplateName}", cancellationToken);
            await TestDatabase.MigrateAsync(
                DatabaseConnectionString(TemplateName),
                cancellationToken);
            await ExecuteOnServerAsync(
                $"ALTER DATABASE {TemplateName} WITH IS_TEMPLATE true ALLOW_CONNECTIONS false",
                cancellationToken);
            _isTemplateReady = true;
        }
        finally
        {
            _templateGate.Release();
        }
    }

    private string DatabaseConnectionString(string database) =>
        new NpgsqlConnectionStringBuilder(ConnectionString) { Database = database }
            .ConnectionString;

    internal async Task ExecuteOnServerAsync(string sql, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
