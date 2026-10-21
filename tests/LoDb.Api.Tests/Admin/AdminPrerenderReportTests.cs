using System.Net;
using System.Text.Json;
using LoDb.Api.Modules.Admin.Prerender.Http.Storage;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Infrastructure.Persistence.Prerender;
using LoDb.Testing;

namespace LoDb.Api.Tests.Admin;

/// <summary>
/// The report of the pre-rendering: the active pass with its pace and estimated end, the
/// served epoch of the settings, the next epoch to serve, and what the store holds, epoch by
/// epoch and version by version, with its estimated monthly cost.
/// </summary>
/// <remarks>
/// A host per test, served epoch 2, its passes written straight into the database: the report
/// reads the counters the engine keeps, whatever wrote them.
/// </remarks>
public sealed class AdminPrerenderReportTests(PostgresContainerFixture postgres) : IAsyncLifetime
{
    private const string Report = "/api/admin/prerender";
    private const long Gigabyte = 1_000_000_000;

    private AccountsApp? _app;

    private AccountsApp App => _app ?? throw new InvalidOperationException("Not started.");

    private DateTimeOffset Now => App.Clock.GetUtcNow();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() =>
        _app = await AccountsApp.StartAsync(
            postgres,
            new Dictionary<string, string?> { ["LoDb:Prerender:ServedEpoch"] = "2" },
            static _ => { });

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    [Fact]
    public async Task ActivePassShowsItsPaceAndEstimatedEnd()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        await SeedAsync();

        var report = await ReadAsync(admin);

        Assert.Equal(2, report.GetProperty("servedEpoch").GetInt32());
        // Epoch 3 holds one unit of a version this host's Data Dragon does not list: no older
        // patch to judge it by, so it is not ready (AdminPrerenderReadinessTests).
        Assert.Equal(JsonValueKind.Null, report.GetProperty("readyEpoch").ValueKind);
        Assert.False(report.GetProperty("isConfigured").GetBoolean());
        Assert.Equal(
            [2, 3, 2, 1],
            report.GetProperty("passes").EnumerateArray()
                .Select(static pass => pass.GetProperty("epoch").GetInt32()));
        var current = report.GetProperty("current");
        Assert.Equal("running", ApiJson.Text(current, "state"));
        Assert.Equal(600, current.GetProperty("activeSeconds").GetInt64());
        Assert.Equal(60.0m, current.GetProperty("pagesPerMinute").GetDecimal());
        var units = current.GetProperty("units");
        Assert.Equal(
            (4, 2, 1, 1),
            (units.GetProperty("total").GetInt32(), units.GetProperty("pending").GetInt32(),
                units.GetProperty("running").GetInt32(),
                units.GetProperty("completed").GetInt32()));
        // 400 pages of the listed units and two pending units at their mean of 500, at 60 a
        // minute.
        var end = current.GetProperty("estimatedEndAt").GetDateTimeOffset();
        Assert.Equal(Now.AddMinutes(1400 / 60.0), end, TimeSpan.FromSeconds(1));
        Assert.Equal(1400, current.GetProperty("pagesLeft").GetInt64());
        Assert.Equal(JsonValueKind.Null, current.GetProperty("pausedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, current.GetProperty("pauseReason").ValueKind);
        Assert.Equal(
            Now.AddMinutes(-10),
            current.GetProperty("runningSince").GetDateTimeOffset(),
            TimeSpan.FromSeconds(1));
    }

    // A report carries 50 passes, read every minute: the versions of each are counted.
    [Fact]
    public async Task ScopeOfAPassIsSummedUp()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        await SeedAsync();

        var passes = (await ReadAsync(admin)).GetProperty("passes").EnumerateArray().ToList();

        var listed = passes[^1].GetProperty("scope");
        Assert.Equal(
            (3, "16.18.1", "16.12.1", false),
            (listed.GetProperty("versionCount").GetInt32(), ApiJson.Text(listed, "newestListed"),
                ApiJson.Text(listed, "oldestListed"),
                listed.GetProperty("skipComplete").GetBoolean()));
        Assert.Equal(["fr"], listed.GetProperty("locales").EnumerateArray()
            .Select(static locale => locale.GetString()));
        Assert.False(listed.TryGetProperty("versions", out _));
        var whole = passes[0].GetProperty("scope");
        Assert.Equal(JsonValueKind.Null, whole.GetProperty("versionCount").ValueKind);
        Assert.Equal(JsonValueKind.Null, whole.GetProperty("locales").ValueKind);
    }

    // The costs to four decimals; the totals leave the retired epoch out, and add the exact
    // costs (0.15257), not the rounded ones.
    [Fact]
    public async Task StorageCountsEachEpochOnceAndPricesItsClass()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        await SeedAsync();

        var storage = (await ReadAsync(admin)).GetProperty("storage");

        Assert.Equal(
            [(3, "standby", 200L, 0.0161m), (2, "served", 500L, 0.0402m),
                (1, "retired", 100L, 0.0080m)],
            storage.GetProperty("epochs").EnumerateArray().Select(static epoch =>
                (epoch.GetProperty("epoch").GetInt32(), ApiJson.Text(epoch, "state")!,
                    epoch.GetProperty("pages").GetInt64(),
                    epoch.GetProperty("monthlyCost").GetDecimal())));
        var served = storage.GetProperty("epochs")[1];
        Assert.True(served.GetProperty("isWriting").GetBoolean());
        Assert.Equal(5 * Gigabyte, served.GetProperty("bytes").GetInt64());
        var version = Assert.Single(storage.GetProperty("servedVersions").EnumerateArray());
        Assert.Equal(
            ("16.18.1", 2, 500L),
            (ApiJson.Text(version, "version")!, version.GetProperty("locales").GetInt32(),
                version.GetProperty("pages").GetInt64()));
        var builds = storage.GetProperty("builds");
        Assert.Equal(0.0964m, builds.GetProperty("monthlyCost").GetDecimal());
        Assert.Equal(120, builds.GetProperty("files").GetInt64());
        Assert.Equal(
            (700L, 13 * Gigabyte, 0.1526m, "EUR", true),
            (storage.GetProperty("pages").GetInt64(), storage.GetProperty("bytes").GetInt64(),
                storage.GetProperty("monthlyCost").GetDecimal(),
                ApiJson.Text(storage, "currency")!,
                storage.GetProperty("excludesEgress").GetBoolean()));
    }

    [Fact]
    public async Task EmptyHistoryShowsTheServedEpochEmpty()
    {
        using var admin = await AdminBrowser.OpenAsync(App);

        var report = await ReadAsync(admin);

        Assert.Equal(JsonValueKind.Null, report.GetProperty("current").ValueKind);
        Assert.Equal(JsonValueKind.Null, report.GetProperty("readyEpoch").ValueKind);
        var epoch = Assert.Single(report.GetProperty("storage").GetProperty("epochs")
            .EnumerateArray());
        Assert.Equal((2, 0L), (epoch.GetProperty("epoch").GetInt32(),
            epoch.GetProperty("pages").GetInt64()));
        Assert.Equal(0m, report.GetProperty("storage").GetProperty("monthlyCost").GetDecimal());
    }

    [Fact]
    public void NegativePriceIsRefusedAtStartup()
    {
        var result = new PrerenderPriceOptionsValidator().Validate(
            null,
            new PrerenderPriceOptions { PricePerGbMonthMultiAz = -0.01m });

        Assert.True(result.Failed);
        Assert.Contains("PricePerGbMonthMultiAz", result.FailureMessage, StringComparison.Ordinal);
    }

    private static async Task<JsonElement> ReadAsync(BrowserClient browser)
    {
        using var response = await browser.GetAsync(Report);
        return await ApiJson.ReadAsync(response, HttpStatusCode.OK);
    }

    // Epoch 1 retired, written by a pass over three listed versions in French; epoch 2 served,
    // written again by the running pass after a cancelled one; epoch 3 complete, waiting for
    // the switch; one build stored.
    private async Task SeedAsync()
    {
        await using var db = App.Database();
        var listed = Finished(
            1,
            PrerenderPassState.Completed,
            30,
            Written("16.17.1", "fr", 100, 1));
        listed.Scope = "{\"versions\":[\"16.17.1\",\"16.12.1\",\"16.18.1\"],"
            + "\"locales\":[\"fr\"]}";
        db.PrerenderPasses.AddRange(
            listed,
            Finished(2, PrerenderPassState.Cancelled, 25, Written("16.18.1", "fr", 300, 3)),
            Finished(3, PrerenderPassState.Completed, 20, Written("16.18.1", "fr", 200, 2)),
            Running());
        db.PrerenderBuilds.Add(new PrerenderBuild
        {
            Hash = new string('a', PrerenderBuild.HashLength),
            Files = 120,
            Bytes = 6 * Gigabyte,
        });
        await db.SaveChangesAsync(Cancellation);
    }

    // 600 pages in ten minutes, 1,000 listed over two units, two units not reached yet.
    private PrerenderPass Running()
    {
        var pass = Pass(2, PrerenderPassState.Running, Now.AddHours(-1));
        pass.RunningSince = Now.AddMinutes(-10);
        (pass.PagesTotal, pass.PagesWritten, pass.PagesSkipped, pass.PagesFailed) =
            (1000, 500, 60, 40);
        var done = Written("16.18.1", "fr", 450, 4.5m);
        (done.State, done.PagesTotal) = (PrerenderUnitState.Completed, 500);
        var current = Written("16.18.1", "en", 50, 0.5m);
        (current.State, current.PagesTotal) = (PrerenderUnitState.Running, 500);
        pass.Units = [done, current, Unit("16.17.1", "fr"), Unit("16.17.1", "en")];
        return pass;
    }

    private PrerenderPass Finished(
        int epoch,
        PrerenderPassState state,
        int daysAgo,
        PrerenderUnit unit)
    {
        var pass = Pass(epoch, state, Now.AddDays(-daysAgo));
        pass.FinishedAt = pass.CreatedAt.AddDays(1);
        pass.Units = [unit];
        return pass;
    }

    private static PrerenderPass Pass(int epoch, PrerenderPassState state, DateTimeOffset at) =>
        new() { Epoch = epoch, State = state, CreatedAt = at };

    private static PrerenderUnit Written(string version, string locale, int pages, decimal gb)
    {
        var unit = Unit(version, locale);
        (unit.State, unit.PagesTotal, unit.PagesWritten, unit.Cursor) =
            (PrerenderUnitState.Completed, pages, pages, pages);
        unit.BytesWritten = (long)(gb * Gigabyte);
        return unit;
    }

    private static PrerenderUnit Unit(string version, string locale) => new()
    {
        Version = version,
        Locale = locale,
        State = PrerenderUnitState.Pending,
        UpdatedAt = DateTimeOffset.UnixEpoch,
    };
}
