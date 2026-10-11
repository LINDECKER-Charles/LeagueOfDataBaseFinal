using LoDb.Testing;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LoDb.Infrastructure.Tests.Persistence;

/// <summary>
/// The migration of the query statistics: <c>pg_stat_statements</c> installed on a new
/// database and on one at the previous migration, on a server that does not preload the
/// library (as the tests' own), and removed by the rollback.
/// </summary>
public sealed class QueryStatisticsMigrationTests(PostgresContainerFixture postgres)
{
    private const string PreviousMigration = "20260926185409_Lot6BillingAnalyticsApps";

    private const string InstalledSql =
        "SELECT extname FROM pg_extension WHERE extname = 'pg_stat_statements'";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task NewDatabaseGetsTheExtension()
    {
        await using var database = await postgres.CreateDatabaseAsync(Cancellation);

        await database.MigrateAsync(Cancellation);

        Assert.Single(await database.QueryAsync(InstalledSql, Cancellation));
    }

    [Fact]
    public async Task DatabaseAtThePreviousMigrationGetsItAndTheRollbackRemovesIt()
    {
        await using var database = await postgres.CreateDatabaseAsync(Cancellation);
        await MigrateToAsync(database, PreviousMigration);
        Assert.Empty(await database.QueryAsync(InstalledSql, Cancellation));

        await database.MigrateAsync(Cancellation);
        Assert.Single(await database.QueryAsync(InstalledSql, Cancellation));

        await MigrateToAsync(database, PreviousMigration);
        Assert.Empty(await database.QueryAsync(InstalledSql, Cancellation));
    }

    private static async Task MigrateToAsync(TestDatabase database, string migration)
    {
        await using var context = database.CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(migration, Cancellation);
    }
}
