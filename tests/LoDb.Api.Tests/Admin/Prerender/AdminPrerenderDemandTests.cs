using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Admin.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using LoDb.Testing;

namespace LoDb.Api.Tests.Admin.Prerender;

/// <summary>
/// The automatic queue as the admin reads it: whether it runs, its counts by state, the pages
/// written lately, its ten most requested pending pages; to an administrator alone.
/// </summary>
public sealed class AdminPrerenderDemandTests(PostgresContainerFixture postgres) : IAsyncLifetime
{
    private const string Demand = "/api/admin/prerender/demand";

    private AccountsApp? _app;

    private AccountsApp App => _app ?? throw new InvalidOperationException("Not started.");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _app = await AccountsApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    [Fact]
    public async Task ReportCountsTheQueueAndListsItsMostRequestedPagesFirst()
    {
        var now = DateTimeOffset.UtcNow;
        await SeedAsync(
            Page("/fr/16.11.1/champions", PrerenderDemandState.Pending, 3),
            Page("/en/16.11.1/items/1036-long-sword", PrerenderDemandState.Pending, 9),
            Page("/en/16.11.1/runes", PrerenderDemandState.Done, 4, now.AddMinutes(-10)),
            Page("/de/16.11.1/runes", PrerenderDemandState.Done, 2, now.AddHours(-5)),
            Page("/es/16.11.1/runes", PrerenderDemandState.Failed, 1),
            Page("/it/16.11.1/runes", PrerenderDemandState.Rendering, 1));
        using var admin = await AdminBrowser.OpenAsync(App);

        using var response = await admin.GetAsync(Demand);

        var report = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        Assert.Equal(1L, report.GetProperty("rendering").GetInt64());
        Assert.Equal(
            (2L, 2L, 1L, 0L, 1L, 2L, 50),
            (report.GetProperty("pending").GetInt64(), report.GetProperty("done").GetInt64(),
                report.GetProperty("failed").GetInt64(), report.GetProperty("skipped").GetInt64(),
                report.GetProperty("writtenLastHour").GetInt64(),
                report.GetProperty("writtenLastDay").GetInt64(),
                report.GetProperty("queueSharePercent").GetInt32()));
        Assert.Equal(
            ["/en/16.11.1/items/1036-long-sword", "/fr/16.11.1/champions"],
            report.GetProperty("top").EnumerateArray()
                .Select(static page => page.GetProperty("path").GetString()));
        Assert.True(report.GetProperty("isEnabled").GetBoolean());
    }

    // Served epoch 1: a page a pass wrote again counts apart, one of another epoch not at all;
    // a page written before its bytes were kept weighs nothing.
    [Fact]
    public async Task ReportWeighsTheServedEpochAndCountsTheLastDayHourByHour()
    {
        var now = App.Clock.GetUtcNow();
        var kept = Page("/en/16.11.1/runes", PrerenderDemandState.Done, 1, now.AddHours(-2));
        kept.Bytes = 3000;
        var unweighed = Page("/fr/16.11.1/runes", PrerenderDemandState.Done, 1, now);
        var rewritten = Page("/de/16.11.1/runes", PrerenderDemandState.Done, 1, now.AddDays(-3));
        (rewritten.Bytes, rewritten.SupersededAt) = (5000, now);
        var elsewhere = Page("/es/16.11.1/runes", PrerenderDemandState.Done, 1, now.AddDays(-3));
        (elsewhere.Bytes, elsewhere.Epoch) = (7000, 2);
        var entered = Page("/it/16.11.1/runes", PrerenderDemandState.Pending, 1);
        PrerenderDemandPage[] seen = [kept, unweighed, rewritten, elsewhere];
        Array.ForEach(seen, page => page.FirstSeenAt = now);
        entered.FirstSeenAt = now.AddHours(-2);
        await SeedAsync([.. seen, entered]);
        using var admin = await AdminBrowser.OpenAsync(App);

        using var response = await admin.GetAsync(Demand);

        var report = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        Assert.Equal(
            (2L, 3000L, 1L),
            (report.GetProperty("servedEpochPages").GetInt64(),
                report.GetProperty("servedEpochBytes").GetInt64(),
                report.GetProperty("superseded").GetInt64()));
        var hours = report.GetProperty("hours").EnumerateArray().ToList();
        Assert.Equal(24, hours.Count);
        Assert.True(hours[^1].GetProperty("start").GetDateTimeOffset() <= now);
        Assert.Equal(
            [(21, 1L, 1L), (23, 4L, 1L)],
            hours.Select(static (hour, index) => (index, hour.GetProperty("entered").GetInt64(),
                    hour.GetProperty("written").GetInt64()))
                .Where(static hour => hour.Item2 + hour.Item3 > 0));
    }

    [Fact]
    public async Task OnlyAnAdministratorWithSecondFactorReadsIt()
    {
        using var visitor = App.Browser();
        using var unenrolled = await AdminCalls.OpenUnenrolledAsync(App);

        using var refused = await visitor.GetAsync(Demand);
        using var withoutFactor = await unenrolled.GetAsync(Demand);

        Assert.Equal(
            "authentication-required",
            await ApiJson.ProblemCodeAsync(refused, HttpStatusCode.Unauthorized));
        Assert.Equal(
            "mfa-required",
            await ApiJson.ProblemCodeAsync(withoutFactor, HttpStatusCode.Forbidden));
    }

    private static PrerenderDemandPage Page(
        string path,
        PrerenderDemandState state,
        long demand,
        DateTimeOffset? renderedAt = null)
    {
        var parts = path.Split('/');
        return new PrerenderDemandPage
        {
            Path = path,
            Version = parts[2],
            Locale = parts[1],
            PageType = parts[3],
            Demand = demand,
            State = state,
            FirstSeenAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
            RenderedAt = renderedAt,
            Epoch = state == PrerenderDemandState.Pending ? null : 1,
        };
    }

    private async Task SeedAsync(params PrerenderDemandPage[] pages)
    {
        await using var db = App.Database();
        db.PrerenderDemand.AddRange(pages);
        await db.SaveChangesAsync(Cancellation);
    }
}
