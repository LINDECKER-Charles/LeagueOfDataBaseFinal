using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace LoDb.Testing;

/// <summary>
/// A throwaway PostgreSQL server, shared by the tests of a class, a collection or an
/// assembly.
/// </summary>
/// <remarks>
/// Same major version and flavour as the production image. The container is removed when the
/// fixture is disposed, and by the Testcontainers reaper if the run dies first. Tests that
/// write share the server, never a database: each creates its own.
/// </remarks>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    public const string Image = "postgres:17-alpine";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image).Build();

    /// <summary>Connection string of the server, for <c>ConnectionStrings:LoDb</c>.</summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>A new empty database, dropped when the returned value is disposed.</summary>
    public async Task<TestDatabase> CreateDatabaseAsync(CancellationToken cancellationToken)
    {
        var name = "test_" + Guid.NewGuid().ToString("N");
        await ExecuteOnServerAsync($"CREATE DATABASE {name}", cancellationToken);
        var connectionString = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Database = name,
        };
        return new TestDatabase(this, name, connectionString.ConnectionString);
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

    public async ValueTask InitializeAsync() =>
        await _container.StartAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    internal async Task ExecuteOnServerAsync(string sql, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
