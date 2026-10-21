using LoDb.Testing;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LoDb.Infrastructure.Tests.Persistence.Prerender;

/// <summary>
/// The migration of the pass diagnostics on a database that already ran passes: the units
/// ended before it get their last change as their end, so that the admin lists their failures;
/// the units still pending or running keep none.
/// </summary>
public sealed class PrerenderDiagnosticsMigrationTests(PostgresContainerFixture postgres)
{
    private const string PreviousMigration = "20261020142031_PrerenderBuildSizes";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UnitsEndedBeforeItEndAtTheirLastChange()
    {
        await using var database = await postgres.CreateDatabaseAsync(Cancellation);
        await using (var context = database.CreateContext())
        {
            await context.GetService<IMigrator>().MigrateAsync(PreviousMigration, Cancellation);
        }

        await database.ExecuteAsync(
            """
            INSERT INTO prerender_pass (id, epoch, state, running_since)
            OVERRIDING SYSTEM VALUE VALUES (1, 1, 'running', now());
            INSERT INTO prerender_unit (pass_id, version, locale, state, updated_at) VALUES
                (1, '16.18.1', 'en', 'completed', '2026-10-20T08:00:00Z'),
                (1, '16.18.1', 'fr', 'failed', '2026-10-20T08:05:00Z'),
                (1, '16.18.1', 'de', 'running', '2026-10-20T08:06:00Z'),
                (1, '16.18.1', 'ja', 'pending', '2026-10-20T08:00:00Z');
            """,
            Cancellation);

        await database.MigrateAsync(Cancellation);

        Assert.Equal(
            ["de NULL", "en 08:00", "fr 08:05", "ja NULL"],
            await database.QueryAsync(
                "SELECT locale || ' ' || COALESCE(to_char(ended_at AT TIME ZONE 'UTC', "
                    + "'HH24:MI'), 'NULL') FROM prerender_unit ORDER BY locale",
                Cancellation));
    }
}
