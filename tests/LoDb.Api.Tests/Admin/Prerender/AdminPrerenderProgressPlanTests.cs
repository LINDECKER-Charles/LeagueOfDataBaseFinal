using LoDb.Testing;

namespace LoDb.Api.Tests.Admin.Prerender;

/// <summary>
/// The monitor's reads, read every five seconds, go through an index of
/// <c>prerender_unit</c> on a pass of 8,400 units among others: the last ended and the failed
/// ones by the partial index on <c>ended_at</c>, the changed versions by the key's prefix.
/// </summary>
/// <remarks>
/// The plans of the SQL the monitor sends (<c>ProgressQueries</c>), on a database seeded as a
/// year of passes would leave it: the pass read and four others as big, analysed.
/// </remarks>
public sealed class AdminPrerenderProgressPlanTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private const int Passes = 5;
    private const int Read = 3;

    private TestDatabase? _database;

    private TestDatabase Database =>
        _database ?? throw new InvalidOperationException("Not started.");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public static TheoryData<string> MonitorReads => new()
    {
        // Recent: the units that ended last.
        $"""
        SELECT * FROM prerender_unit WHERE pass_id = {Read} AND ended_at IS NOT NULL
        ORDER BY ended_at DESC LIMIT 12
        """,
        // Failures, and their count.
        $"""
        SELECT * FROM prerender_unit WHERE pass_id = {Read} AND ended_at IS NOT NULL
        AND (state = 'failed' OR pages_failed > 0) ORDER BY ended_at DESC LIMIT 20
        """,
        $"""
        SELECT count(*) FROM prerender_unit WHERE pass_id = {Read} AND ended_at IS NOT NULL
        AND (state = 'failed' OR pages_failed > 0)
        """,
        // The versions changed in the last minute, read whole.
        $"""
        SELECT * FROM prerender_unit WHERE pass_id = {Read} AND version IN (
            SELECT DISTINCT version FROM prerender_unit
            WHERE pass_id = {Read} AND updated_at > now() - interval '1 minute')
        """,
    };

    public async ValueTask InitializeAsync() =>
        _database = await postgres.CreateMigratedDatabaseAsync(Cancellation);

    public async ValueTask DisposeAsync()
    {
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }

    [Theory]
    [MemberData(nameof(MonitorReads))]
    public async Task MonitorReadScansNoUnitTable(string sql)
    {
        await SeedAsync();

        var plan = string.Join('\n', await Database.QueryAsync("EXPLAIN " + sql, Cancellation));

        Assert.Contains("on prerender_unit", plan, StringComparison.Ordinal);
        Assert.DoesNotContain("Seq Scan on prerender_unit", plan, StringComparison.Ordinal);
    }

    // Five passes of 400 versions in 21 locales, each unit ended a minute after the previous
    // one, one in fifty failed; the last ones of the pass read changed a moment ago.
    private async Task SeedAsync()
    {
        await Database.ExecuteAsync(
            $"""
            INSERT INTO prerender_pass (id, epoch, state, finished_at, running_since)
            OVERRIDING SYSTEM VALUE
            SELECT pass, 1, 'completed', now(), NULL FROM generate_series(1, {Passes}) AS pass;
            INSERT INTO prerender_unit (pass_id, version, locale, state, pages_total,
                pages_written, pages_failed, cursor, updated_at, ended_at)
            SELECT pass, '16.' || version || '.1', 'l' || locale,
                CASE WHEN version % 50 = 0 THEN 'failed' ELSE 'completed' END,
                500, 500, 0, 500,
                now() - make_interval(mins => 8421 - version * 21 - locale),
                now() - make_interval(mins => 8421 - version * 21 - locale)
            FROM generate_series(1, {Passes}) AS pass,
                generate_series(1, 400) AS version,
                generate_series(1, 21) AS locale;
            ANALYZE prerender_unit;
            """,
            Cancellation);
    }
}
