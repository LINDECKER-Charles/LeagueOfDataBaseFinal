using System.Globalization;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Baseline;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace LoDb.Testing;

/// <summary>
/// An empty database of a <see cref="PostgresContainerFixture"/>, created for one test and
/// dropped with it.
/// </summary>
public sealed class TestDatabase : IAsyncDisposable
{
    /// <summary>
    /// Tables the new stack adds to the Doctrine schema: the EF history, then the tables of
    /// lot 1. The same list as tools/next/schema/check.sh.
    /// </summary>
    public static readonly IReadOnlyList<string> NewTables =
        ["__EFMigrationsHistory", "ddragon_asset", "ddragon_version", "periodic_job"];

    private readonly PostgresContainerFixture _server;

    internal TestDatabase(PostgresContainerFixture server, string name, string connectionString)
    {
        _server = server;
        Name = name;
        ConnectionString = connectionString;
        DataSource = NpgsqlDataSource.Create(connectionString);
    }

    public string Name { get; }

    public string ConnectionString { get; }

    public NpgsqlDataSource DataSource { get; }

    /// <summary>A context configured as the API configures it.</summary>
    public LoDbDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LoDbDbContext>();
        options.UseLoDb(DataSource);
        return new LoDbDbContext(options.Options);
    }

    /// <summary>A migrator on the given context, as one run of <c>migrate</c>.</summary>
    public static DatabaseMigrator CreateMigrator(LoDbDbContext context) =>
        new(context, TimeProvider.System, NullLogger<DatabaseMigrator>.Instance);

    /// <summary>Brings the database to the latest migration, as <c>migrate</c> does.</summary>
    public async Task<MigrationReport> MigrateAsync(CancellationToken cancellationToken)
    {
        await using var context = CreateContext();
        return await CreateMigrator(context).MigrateAsync(cancellationToken);
    }

    /// <summary>
    /// The database as the Doctrine migrations leave it: the frozen schema and the 11 rows of
    /// the legacy history.
    /// </summary>
    public async Task CreateDoctrineSchemaAsync(CancellationToken cancellationToken)
    {
        await ExecuteAsync(LegacySchema.DoctrineSchemaSql, cancellationToken);
        await using var command = DataSource.CreateCommand(
            """
            INSERT INTO doctrine_migration_versions (version, executed_at, execution_time)
            SELECT version, now()::timestamp(0), 12 FROM unnest(@versions) AS version
            """);
        command.Parameters.AddWithValue("versions", LegacySchema.DoctrineVersions.ToArray());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ExecuteAsync(string sql, CancellationToken cancellationToken)
    {
        await using var command = DataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>The first column of every row, as text (<c>NULL</c> for null).</summary>
    public async Task<IReadOnlyList<string>> QueryAsync(
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = DataSource.CreateCommand(sql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(reader.IsDBNull(0)
                ? "NULL"
                : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture)!);
        }

        return rows;
    }

    /// <summary>Names of the tables of the public schema, sorted.</summary>
    public Task<IReadOnlyList<string>> TablesAsync(CancellationToken cancellationToken) =>
        QueryAsync(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'public'"
            + " ORDER BY tablename COLLATE \"C\"",
            cancellationToken);

    /// <summary>Ids of the EF history, sorted; empty when the table does not exist.</summary>
    public async Task<IReadOnlyList<string>> AppliedMigrationsAsync(
        CancellationToken cancellationToken) =>
        (await TablesAsync(cancellationToken)).Contains("__EFMigrationsHistory")
            ? await QueryAsync(
                "SELECT migration_id FROM \"__EFMigrationsHistory\" ORDER BY migration_id",
                cancellationToken)
            : [];

    /// <summary>The normalized schema dump, without the given tables.</summary>
    public Task<IReadOnlyList<string>> DumpSchemaAsync(
        IEnumerable<string> excludedTables,
        CancellationToken cancellationToken) =>
        _server.DumpSchemaAsync(Name, excludedTables, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await DataSource.DisposeAsync();
        await _server.ExecuteOnServerAsync(
            $"DROP DATABASE IF EXISTS {Name} WITH (FORCE)",
            CancellationToken.None);
    }
}
