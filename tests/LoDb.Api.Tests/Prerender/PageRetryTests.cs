using System.Diagnostics.Metrics;
using System.Net;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Passes;
using LoDb.Api.Modules.Admin.Prerender.Rendering;
using LoDb.Api.Modules.Admin.Prerender.Telemetry;
using LoDb.Api.Tests.Prerender.Support;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.PageStore.Errors;
using LoDb.Infrastructure.Persistence.Prerender;
using LoDb.Testing;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;

namespace LoDb.Api.Tests.Prerender;

/// <summary>
/// What may not last is tried again: the SSR's refusals after their Retry-After, failures a
/// little later, then counted failed. What refuses the pass itself pauses it, uncounted.
/// </summary>
public sealed class PageRetryTests(PostgresContainerFixture postgres)
{
    private static readonly PatchVersion Version = PatchVersion.Parse("16.8.1");
    private static readonly string First = $"/en/{Version.Value}/champions";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RefusalIsWaitedOutAsItsRetryAfterAsks()
    {
        await using var harness = await StartAsync();
        using var pages = Pages(harness);
        harness.Ssr.Answer(
            First,
            () => FakeSsr.Status(HttpStatusCode.ServiceUnavailable, retryAfterSeconds: 7),
            () => FakeSsr.Status(HttpStatusCode.TooManyRequests, retryAfterSeconds: 3));
        var passId = await harness.LaunchAsync(Scope());

        await harness.RunAsync();

        Assert.Equal(
            [TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(3)],
            harness.Clock.Waits.Where(static wait => wait >= TimeSpan.FromSeconds(1)));
        var unit = Assert.Single((await harness.PassAsync(passId)).Units);
        Assert.Equal((6, 0), (unit.PagesWritten, unit.PagesFailed));
        Assert.Equal(2, Count(pages, "refused"));
        Assert.Equal(6, Count(pages, "written"));
    }

    [Fact]
    public async Task ServerErrorIsTriedAgainLaterThenCountedFailed()
    {
        await using var harness = await StartAsync();
        using var pages = Pages(harness);
        harness.Ssr.Answer(
            First,
            () => FakeSsr.Status(HttpStatusCode.InternalServerError),
            () => FakeSsr.Status(HttpStatusCode.BadGateway),
            () => FakeSsr.Status(HttpStatusCode.GatewayTimeout));
        var passId = await harness.LaunchAsync(Scope());

        await harness.RunAsync();

        Assert.Equal(
            [TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1)],
            harness.Clock.Waits.Where(static wait => wait >= TimeSpan.FromSeconds(1)));
        var pass = await harness.PassAsync(passId);
        var unit = Assert.Single(pass.Units);
        Assert.Equal((5, 1, 6), (unit.PagesWritten, unit.PagesFailed, unit.Cursor));
        Assert.Equal(PrerenderPassState.Completed, pass.State);
        Assert.Equal(1, Count(pages, "failed"));
        var summary = Assert.Single(
            harness.Logs.GetSnapshot(),
            static record => record.Id.Name == "prerender.unit.completed");
        Assert.Contains("504", summary.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SsrOutOfReachIsWaitedOutWithoutFailingThePageAndLoggedOnce()
    {
        await using var harness = await StartAsync();
        using var pages = Pages(harness);
        var outage = RetryDelays.MaxAttempts + 2;
        harness.Ssr.Answer(
            First,
            [.. Enumerable.Repeat<Func<HttpResponseMessage>>(Refused, outage)]);
        var passId = await harness.LaunchAsync(Scope());

        await harness.RunAsync();

        var unit = Assert.Single((await harness.PassAsync(passId)).Units);
        Assert.Equal((6, 0), (unit.PagesWritten, unit.PagesFailed));
        Assert.Equal(outage, harness.Clock.Waits.Count(static wait => wait == RetryDelays.Default));
        Assert.Equal((0, 0), (Count(pages, "failed"), Count(pages, "refused")));
        var reach = harness.Logs.GetSnapshot()
            .Where(static record =>
                record.Id.Name?.StartsWith("prerender.ssr.", StringComparison.Ordinal) == true)
            .Select(static record => (record.Id.Name, record.Level));
        Assert.Equal(
            [
                ("prerender.ssr.unreachable", LogLevel.Warning),
                ("prerender.ssr.reachable", LogLevel.Information),
            ],
            reach);

        static HttpResponseMessage Refused() =>
            throw new HttpRequestException(HttpRequestError.ConnectionError, "Connection refused");
    }

    [Fact]
    public async Task RenderMissingPartOfItselfIsTriedAgainNotSkipped()
    {
        await using var harness = await StartAsync();
        harness.Ssr.Answer(First, () => FakeSsr.Unmarked(First, "private, no-store"));
        var passId = await harness.LaunchAsync(Scope());

        await harness.RunAsync();

        var unit = Assert.Single((await harness.PassAsync(passId)).Units);
        Assert.Equal((6, 0), (unit.PagesWritten, unit.PagesSkipped));
        Assert.Contains(TimeSpan.FromSeconds(10), harness.Clock.Waits);
    }

    [Fact]
    public async Task StoreRefusingTheWriterPausesThePassWithoutCountingThePage()
    {
        await using var harness = await StartAsync();
        harness.Store!.FailNext("pages/", new PageStoreConfigurationException(
            "PUT /lodb-pages-test/pages/1/en/16.8.1/champions answered 403 (AccessDenied).",
            HttpStatusCode.Forbidden));
        var passId = await harness.LaunchAsync(Scope());

        await harness.RunAsync();

        var paused = await harness.PassAsync(passId);
        Assert.Equal(PrerenderPassState.Paused, paused.State);
        var unit = Assert.Single(paused.Units);
        Assert.Equal(
            (0, 0, 0, 0),
            (unit.Cursor, unit.PagesWritten, unit.PagesSkipped, unit.PagesFailed));
        Assert.Contains(
            harness.Logs.GetSnapshot(),
            static record => record.Id.Name == "prerender.pass.paused"
                && record.Message.Contains("AccessDenied", StringComparison.Ordinal));

        await harness.Get<PassControl>().ResumeAsync(passId, Token);
        await harness.RunAsync();

        var resumed = await harness.PassAsync(passId);
        Assert.Equal((PrerenderPassState.Completed, 6L), (resumed.State, resumed.PagesWritten));
    }

    [Fact]
    public async Task SsrRefusingThePassItselfPausesIt()
    {
        await using var harness = await StartAsync();
        harness.Ssr.Answer(First, () => FakeSsr.Status(HttpStatusCode.BadRequest));
        var passId = await harness.LaunchAsync(Scope());

        await harness.RunAsync();

        Assert.Equal(PrerenderPassState.Paused, (await harness.PassAsync(passId)).State);
        Assert.Equal([First], harness.Ssr.Pages);
    }

    [Fact]
    public async Task PageTheStoreRefusesFailsAloneAtOnce()
    {
        await using var harness = await StartAsync();
        harness.Store!.FailNext("pages/", new PageStoreRefusedException(
            "The metadata of pages/1/en/16.8.1/champions weigh 2100 bytes, over 2048."));
        var passId = await harness.LaunchAsync(Scope());

        await harness.RunAsync();

        var pass = await harness.PassAsync(passId);
        var unit = Assert.Single(pass.Units);
        Assert.Equal((5, 1), (unit.PagesWritten, unit.PagesFailed));
        Assert.Equal(PrerenderPassState.Completed, pass.State);
        Assert.Single(harness.Ssr.Pages, static path => path == First);
    }

    [Fact]
    public async Task ConflictOnABundleIsTriedAgain()
    {
        await using var harness = await StartAsync();
        harness.Store!.FailNext("build/", new PageStoreTransientException(
            "PUT build/main-ABCD1234.js answered 409 (ConditionalRequestConflict).",
            HttpStatusCode.Conflict));
        var passId = await harness.LaunchAsync(Scope());

        await harness.RunAsync();

        var unit = Assert.Single((await harness.PassAsync(passId)).Units);
        Assert.Equal((6, 0), (unit.PagesWritten, unit.PagesFailed));
        Assert.Contains("build/main-ABCD1234.js", harness.Store.Objects.Keys);
        Assert.Contains(TimeSpan.FromSeconds(10), harness.Clock.Waits);
    }

    private static PassScope Scope() => PassScope.Of(Version) with { Locale = UiLocale.En };

    private static MetricCollector<long> Pages(PrerenderHarness harness) =>
        new(harness.Get<IMeterFactory>(), PrerenderMetrics.MeterName, "lodb.prerender.pages");

    private static long Count(MetricCollector<long> pages, string outcome) =>
        pages.GetMeasurementSnapshot()
            .Where(measurement => Equals(measurement.Tags[PrerenderMetrics.OutcomeTag], outcome))
            .Sum(static measurement => measurement.Value);

    private async Task<PrerenderHarness> StartAsync()
    {
        var harness = await PrerenderHarness.StartAsync(postgres);
        harness.Catalog.Pinned.Add(Version);
        return harness;
    }
}
