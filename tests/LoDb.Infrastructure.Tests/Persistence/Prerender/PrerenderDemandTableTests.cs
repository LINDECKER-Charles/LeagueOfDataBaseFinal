using LoDb.Infrastructure.Persistence.Prerender.Demand;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LoDb.Infrastructure.Tests.Persistence.Prerender;

/// <summary>
/// The table of the automatic queue: one row per page, rows the queue could not make sense
/// of refused, its states round-tripped, and its two partial indexes.
/// </summary>
public sealed class PrerenderDemandTableTests(PostgresContainerFixture postgres)
    : MigratedDatabase(postgres)
{
    private const string Path = "/fr/16.11.1/items/1036-long-sword";

    public static TheoryData<string, string> InvalidRows => new()
    {
        { "'stopped', 0, 0, 0", "ck_prerender_demand_state" },
        { "'pending', -1, 0, 0", "ck_prerender_demand_counters" },
        { "'pending', 0, -1, 0", "ck_prerender_demand_counters" },
        { "'pending', 0, 0, -1", "ck_prerender_demand_counters" },
    };

    [Theory]
    [MemberData(nameof(InvalidRows))]
    public async Task InvalidRowIsRefused(string values, string constraint)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(values));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal(constraint, error.ConstraintName);
    }

    [Fact]
    public async Task PageIsKeyedByItsPathAndItsStateSurvivesARoundTrip()
    {
        await InsertAsync("'done', 7, 7, 1");

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertAsync("'pending', 1, 1, 0"));
        await using var context = Database.CreateContext();
        var page = await context.PrerenderDemand.SingleAsync(Cancellation);

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal(
            (PrerenderDemandState.Done, 7L, 1, "items"),
            (page.State, page.Demand, page.Attempts, page.PageType));
    }

    [Fact]
    public async Task WorkerAndAdminReadThroughPartialIndexes()
    {
        var indexes = await Database.QueryAsync(
            """
            SELECT indexdef FROM pg_indexes
            WHERE tablename = 'prerender_demand' AND indexname LIKE 'ix_%'
            ORDER BY indexname
            """,
            Cancellation);

        Assert.Equal(2, indexes.Count);
        Assert.Contains("demand DESC", indexes[0], StringComparison.Ordinal);
        Assert.Contains("'pending'", indexes[0], StringComparison.Ordinal);
        Assert.Contains("rendered_at IS NOT NULL", indexes[1], StringComparison.Ordinal);
    }

    private Task InsertAsync(string values) => Database.ExecuteAsync(
        "INSERT INTO prerender_demand (path, version, locale, page_type, first_seen_at,"
            + " last_seen_at, state, demand, ssr_count, attempts)"
            + $" VALUES ('{Path}', '16.11.1', 'fr', 'items', now(), now(), {values})",
        Cancellation);
}
