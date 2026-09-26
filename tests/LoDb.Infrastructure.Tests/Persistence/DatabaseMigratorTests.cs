using LoDb.Infrastructure.Persistence.Baseline;
using LoDb.Testing;

namespace LoDb.Infrastructure.Tests.Persistence;

/// <summary>
/// <c>migrate</c> and <c>baseline mark-applied</c>: an empty database gets every migration,
/// a Doctrine database is marked at the baseline then migrated, twice changes nothing, and
/// any other database is refused untouched.
/// </summary>
public sealed class DatabaseMigratorTests(PostgresContainerFixture postgres)
{
    private const string Lot1 = "20260926022150_Lot1DataDragon";
    private const string EfHistory = "__EFMigrationsHistory";

    private static readonly string[] AllMigrations = [DoctrineBaseline.MigrationId, Lot1];

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public static TheoryData<string, string> UnexpectedSchemas => new()
    {
        {
            "ALTER TABLE users ADD COLUMN nickname varchar(32)",
            "+ column users.nickname character varying(32)"
        },
        {
            "DROP INDEX idx_ab264a57e3c61f9",
            "- index builds.idx_ab264a57e3c61f9"
                + " CREATE INDEX idx_ab264a57e3c61f9 ON builds USING btree (owner_id)"
        },
        {
            "ALTER TABLE api_keys ALTER COLUMN plan SET DEFAULT 'pro'",
            "+ column api_keys.plan character varying(16) not null"
                + " default 'pro'::character varying"
        },
        {
            "CREATE TABLE ddragon_asset (version text PRIMARY KEY)",
            "+ table ddragon_asset"
        },
        {
            @"DELETE FROM doctrine_migration_versions WHERE version LIKE '%20260719150000'",
            @"- doctrine migration DoctrineMigrations\Version20260719150000"
        },
        {
            @"INSERT INTO doctrine_migration_versions (version)"
                + @" VALUES ('DoctrineMigrations\Version20260801000000')",
            @"+ doctrine migration DoctrineMigrations\Version20260801000000"
        },
    };

    [Fact]
    public async Task EmptyDatabaseGetsEveryMigration()
    {
        await using var database = await postgres.CreateDatabaseAsync(Cancellation);

        var report = await database.MigrateAsync(Cancellation);

        Assert.Equal(BaselineOutcome.EmptyDatabase, report.Baseline);
        Assert.Equal(AllMigrations, report.Applied);
        Assert.Equal(AllMigrations, await database.AppliedMigrationsAsync(Cancellation));
    }

    [Fact]
    public async Task DoctrineDatabaseIsMarkedThenMigrated()
    {
        await using var database = await postgres.CreateDatabaseAsync(Cancellation);
        await database.CreateDoctrineSchemaAsync(Cancellation);

        var report = await database.MigrateAsync(Cancellation);

        Assert.Equal(BaselineOutcome.Marked, report.Baseline);
        Assert.Equal([Lot1], report.Applied);
        Assert.Equal(AllMigrations, await database.AppliedMigrationsAsync(Cancellation));
        Assert.Equal(
            LegacySchema.DoctrineSchema,
            await database.DumpSchemaAsync(TestDatabase.NewTables, Cancellation));
    }

    [Fact]
    public async Task SecondMigrateChangesNothing()
    {
        await using var database = await postgres.CreateDatabaseAsync(Cancellation);
        await database.CreateDoctrineSchemaAsync(Cancellation);
        await database.MigrateAsync(Cancellation);
        var schema = await database.DumpSchemaAsync([], Cancellation);
        var history = await HistoryRowsAsync(database);

        var report = await database.MigrateAsync(Cancellation);

        Assert.Equal(BaselineOutcome.AlreadyApplied, report.Baseline);
        Assert.Empty(report.Applied);
        Assert.Equal(schema, await database.DumpSchemaAsync([], Cancellation));
        Assert.Equal(history, await HistoryRowsAsync(database));
    }

    [Theory]
    [MemberData(nameof(UnexpectedSchemas))]
    public async Task UnexpectedSchemaIsRefusedUntouched(string alteration, string difference)
    {
        await using var database = await postgres.CreateDatabaseAsync(Cancellation);
        await database.CreateDoctrineSchemaAsync(Cancellation);
        await database.ExecuteAsync(alteration, Cancellation);
        var schema = await database.DumpSchemaAsync([], Cancellation);

        var report = await database.MigrateAsync(Cancellation);

        Assert.Equal(BaselineOutcome.UnexpectedSchema, report.Baseline);
        Assert.Contains(difference, report.Differences);
        Assert.Empty(report.Applied);
        Assert.Equal(schema, await database.DumpSchemaAsync([], Cancellation));
        Assert.DoesNotContain(EfHistory, await database.TablesAsync(Cancellation));
    }

    [Fact]
    public async Task MarkBaselineAppliedIsIdempotent()
    {
        await using var database = await postgres.CreateDatabaseAsync(Cancellation);
        await database.CreateDoctrineSchemaAsync(Cancellation);

        var first = await MarkAsync(database);
        var second = await MarkAsync(database);

        Assert.Equal(BaselineOutcome.Marked, first.Baseline);
        Assert.Equal(BaselineOutcome.AlreadyApplied, second.Baseline);
        Assert.Equal(
            [DoctrineBaseline.MigrationId],
            await database.AppliedMigrationsAsync(Cancellation));
        Assert.Equal(
            LegacySchema.DoctrineSchema,
            await database.DumpSchemaAsync([EfHistory], Cancellation));
    }

    [Fact]
    public async Task MarkBaselineAppliedLeavesAnEmptyDatabaseEmpty()
    {
        await using var database = await postgres.CreateDatabaseAsync(Cancellation);

        var report = await MarkAsync(database);

        Assert.Equal(BaselineOutcome.EmptyDatabase, report.Baseline);
        Assert.Empty(await database.TablesAsync(Cancellation));
    }

    [Fact]
    public async Task ConcurrentMigrationsMarkTheBaselineOnce()
    {
        await using var database = await postgres.CreateDatabaseAsync(Cancellation);
        await database.CreateDoctrineSchemaAsync(Cancellation);

        var reports = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => database.MigrateAsync(Cancellation)));

        Assert.Single(reports, static report => report.Baseline is BaselineOutcome.Marked);
        Assert.All(
            reports,
            static report => Assert.Contains(
                report.Baseline,
                new[] { BaselineOutcome.Marked, BaselineOutcome.AlreadyApplied }));
        Assert.Equal(AllMigrations, await database.AppliedMigrationsAsync(Cancellation));
    }

    private static async Task<MigrationReport> MarkAsync(TestDatabase database)
    {
        await using var context = database.CreateContext();
        return await TestDatabase.CreateMigrator(context).MarkBaselineAppliedAsync(Cancellation);
    }

    private static Task<IReadOnlyList<string>> HistoryRowsAsync(TestDatabase database) =>
        database.QueryAsync(
            "SELECT migration_id || ' ' || product_version FROM \"__EFMigrationsHistory\""
                + " ORDER BY migration_id",
            Cancellation);
}
