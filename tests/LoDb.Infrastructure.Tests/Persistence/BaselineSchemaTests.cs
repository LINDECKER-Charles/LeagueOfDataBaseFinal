using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Baseline;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LoDb.Infrastructure.Tests.Persistence;

/// <summary>
/// The Baseline migration reproduces the schema of the Doctrine migrations: same
/// <c>pg_dump</c>, same catalog, same legacy history. The new stack only adds the EF history
/// and the tables of its lots.
/// </summary>
public sealed class BaselineSchemaTests(PostgresContainerFixture postgres)
{
    private const string TablePrefix = "table ";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task BaselineOnAnEmptyDatabaseDumpsAsTheDoctrineSchema()
    {
        await using var database = await postgres.CreateDatabaseAsync(Cancellation);
        await MigrateToBaselineAsync(database);

        var dump = await database.DumpSchemaAsync(["__EFMigrationsHistory"], Cancellation);
        var versions = await database.QueryAsync(
            "SELECT version FROM doctrine_migration_versions ORDER BY version",
            Cancellation);

        Assert.Equal(LegacySchema.DoctrineSchema, dump);
        Assert.Equal(LegacySchema.DoctrineVersions, versions);
    }

    [Fact]
    public async Task EveryMigrationOnlyAddsTheNewTables()
    {
        await using var database = await postgres.CreateDatabaseAsync(Cancellation);
        await database.MigrateAsync(Cancellation);

        var dump = await database.DumpSchemaAsync(TestDatabase.NewTables, Cancellation);
        var tables = await database.TablesAsync(Cancellation);

        Assert.Equal(LegacySchema.DoctrineSchema, dump);
        Assert.Equal(
            TestDatabase.NewTables.Order(StringComparer.Ordinal),
            tables.Except(DoctrineTables(), StringComparer.Ordinal));
    }

    [Fact]
    public async Task FrozenDoctrineSchemaHasTheEmbeddedCatalog()
    {
        await using var database = await postgres.CreateDatabaseAsync(Cancellation);
        await database.CreateDoctrineSchemaAsync(Cancellation);

        Assert.Equal(SchemaCatalog.Doctrine, await ReadCatalogAsync(database));
    }

    [Fact]
    public async Task BaselineHasTheEmbeddedCatalog()
    {
        await using var database = await postgres.CreateDatabaseAsync(Cancellation);
        await MigrateToBaselineAsync(database);

        Assert.Equal(SchemaCatalog.Doctrine, await ReadCatalogAsync(database));
    }

    [Fact]
    public void BaselineWritesTheFrozenDoctrineHistory() =>
        Assert.Equal(LegacySchema.DoctrineVersions, DoctrineBaseline.DoctrineVersions);

    [Fact]
    public void ModelMatchesTheLastMigration()
    {
        using var context = new LoDbDesignTimeFactory().CreateDbContext([]);

        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Equal(DoctrineBaseline.MigrationId, context.Database.GetMigrations().First());
    }

    internal static async Task<IReadOnlyList<string>> ReadCatalogAsync(TestDatabase database)
    {
        await using var connection = await database.DataSource.OpenConnectionAsync(Cancellation);
        return await SchemaCatalog.ReadAsync(connection, null, SchemaCatalog.Query, Cancellation);
    }

    private static IEnumerable<string> DoctrineTables() =>
        SchemaCatalog.Doctrine
            .Where(static line => line.StartsWith(TablePrefix, StringComparison.Ordinal))
            .Select(static line => line[TablePrefix.Length..]);

    private static async Task MigrateToBaselineAsync(TestDatabase database)
    {
        await using var context = database.CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(
            DoctrineBaseline.MigrationId,
            Cancellation);
    }
}
