using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Baseline;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LoDb.Infrastructure.Tests.Persistence;

/// <summary>
/// The Baseline migration reproduces the schema of the Doctrine migrations: same
/// <c>pg_dump</c>, same catalog, same legacy history. The new stack only adds the EF history,
/// the tables of its lots and the frozen columns of lot 4 on <c>users</c>; lot 6 adds tables
/// only.
/// </summary>
public sealed class BaselineSchemaTests(PostgresContainerFixture postgres)
{
    private const string TablePrefix = "table ";
    private const string AddedPrefix = "+ ";

    /// <summary>
    /// Tables of lot 4. Not in <see cref="TestDatabase.NewTables"/>, which mirrors
    /// <c>tools/next/schema/check.sh</c>: that script compares whole dumps, which cannot match
    /// once <c>users</c> has the columns of lot 4.
    /// </summary>
    internal static readonly string[] Lot4Tables =
    [
        "audit_log", "data_protection_keys", "email_outbox", "identity_roles",
        "identity_user_roles", "identity_user_tokens",
    ];

    /// <summary>Tables of lot 6, for lots 6, 7, 9 and 10; like lot 4's, not in NewTables.</summary>
    internal static readonly string[] Lot6Tables =
    [
        "analytics_daily", "analytics_event", "api_credit_grants", "client_policy",
        "stripe_event",
    ];

    // Everything a migration adds to a Doctrine table, frozen: Identity's columns on users
    // (nullable, with a default or generated, so that the legacy stack keeps inserting rows)
    // and the indexes of the generated ones. The catalog shows a generated column as a default.
    private static readonly string[] LegacyTableAdditions =
    [
        "column users.access_failed_count integer not null default 0",
        "column users.concurrency_stamp text",
        "column users.lockout_enabled boolean not null default true",
        "column users.lockout_end timestamp with time zone",
        "column users.normalized_email text default upper((email)::text)",
        "column users.normalized_username text default upper((username)::text)",
        "column users.security_stamp text",
        "column users.two_factor_enabled boolean not null default false",
        "index users.ix_users_normalized_email CREATE INDEX ix_users_normalized_email"
            + " ON users USING btree (normalized_email)",
        "index users.ix_users_normalized_username CREATE INDEX ix_users_normalized_username"
            + " ON users USING btree (normalized_username)",
    ];

    // Catalog lines named after their table, as "column users.email ...".
    private static readonly string[] TableObjectPrefixes =
        ["column ", "constraint ", "index ", "trigger "];

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
    public async Task EveryMigrationOnlyAddsToTheDoctrineSchema()
    {
        await using var database = await postgres.CreateDatabaseAsync(Cancellation);
        await database.MigrateAsync(Cancellation);

        var tables = await database.TablesAsync(Cancellation);

        await AssertOnlyAdditionsAsync(database);
        Assert.Equal(
            NewTables().Order(StringComparer.Ordinal),
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

    /// <summary>
    /// The schema of <paramref name="database"/> keeps every object of the Doctrine one,
    /// unchanged, and adds only the new tables and <see cref="LegacyTableAdditions"/>.
    /// </summary>
    internal static async Task AssertOnlyAdditionsAsync(TestDatabase database)
    {
        var differences = SchemaCatalog.Compare(
            SchemaCatalog.Doctrine,
            await ReadCatalogAsync(database));

        Assert.All(
            differences,
            static line => Assert.StartsWith(AddedPrefix, line, StringComparison.Ordinal));
        Assert.Equal(
            LegacyTableAdditions,
            differences
                .Select(static line => line[AddedPrefix.Length..])
                .Where(static line => !IsOnNewTable(line)));
    }

    private static IEnumerable<string> NewTables() =>
        TestDatabase.NewTables.Concat(Lot4Tables).Concat(Lot6Tables);

    // The table itself, its columns, constraints, indexes and triggers, and the sequences of
    // its identity columns, which PostgreSQL names after it.
    private static bool IsOnNewTable(string line) =>
        NewTables().Any(table =>
            line == TablePrefix + table
            || line.StartsWith($"sequence {table}_", StringComparison.Ordinal)
            || TableObjectPrefixes.Any(prefix =>
                line.StartsWith($"{prefix}{table}.", StringComparison.Ordinal)));

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
