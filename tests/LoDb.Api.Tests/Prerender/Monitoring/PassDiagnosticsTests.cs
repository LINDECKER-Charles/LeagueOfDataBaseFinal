using System.Net;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Passes;
using LoDb.Api.Modules.Admin.Prerender.Units;
using LoDb.Api.Tests.Prerender.Support;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.PageStore.Errors;
using LoDb.Infrastructure.Persistence.Prerender;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Testing;

namespace LoDb.Api.Tests.Prerender.Monitoring;

/// <summary>
/// What a pass keeps for the admin as it goes: since when and why it is paused, when each unit
/// started and ended, and why a page or a unit failed. None of it changes what the pass
/// renders or counts.
/// </summary>
public sealed class PassDiagnosticsTests(PostgresContainerFixture postgres)
{
    private static readonly PatchVersion Version = PatchVersion.Parse("16.7.1");
    private static readonly PatchVersion Older = PatchVersion.Parse("16.6.1");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // The pause moves the pass alone: its unit stays running, and is taken again first.
    [Fact]
    public async Task PauseIsDatedAndItsUnitResumesFirstKeepingItsStart()
    {
        await using var harness = await StartAsync();
        harness.Catalog.Pinned.Add(Older);
        var passId = await harness.LaunchAsync(new PassScope { Locales = [UiLocale.En] });
        harness.Ssr.OnPage = async path =>
        {
            if (path == PathOf(Version, "runes"))
            {
                await harness.Get<PassControl>().PauseAsync(passId, Token);
            }
        };

        await harness.RunAsync();
        var paused = await harness.PassAsync(passId);
        harness.Ssr.OnPage = null;
        harness.Clock.Advance(TimeSpan.FromMinutes(5));
        await harness.Get<PassControl>().ResumeAsync(passId, Token);
        var resumed = await harness.PassAsync(passId);
        var before = harness.Ssr.Pages.Count;
        await harness.RunAsync();

        Assert.NotNull(paused.PausedAt);
        Assert.Null(paused.PauseReason);
        var stopped = UnitOf(paused, Version);
        Assert.Equal(PrerenderUnitState.Running, stopped.State);
        Assert.NotNull(stopped.StartedAt);
        Assert.Null(stopped.EndedAt);
        Assert.Null(resumed.PausedAt);
        Assert.Equal(PathOf(Version, "summoners"), harness.Ssr.Pages[before]);
        var done = UnitOf(await harness.PassAsync(passId), Version);
        Assert.Equal(stopped.StartedAt, done.StartedAt);
        Assert.NotNull(done.EndedAt);
    }

    [Fact]
    public async Task EndOfAUnitIsWrittenOnce()
    {
        await using var harness = await StartAsync();
        var passId = await harness.LaunchAsync(Scope());
        await harness.RunAsync();
        var ended = UnitOf(await harness.PassAsync(passId), Version).EndedAt;
        harness.Clock.Advance(TimeSpan.FromHours(1));

        await harness.Get<UnitQueue>().FinishAsync(
            Claimed(passId),
            PrerenderUnitState.Completed,
            Token);

        Assert.NotNull(ended);
        Assert.Equal(ended, UnitOf(await harness.PassAsync(passId), Version).EndedAt);
    }

    [Fact]
    public async Task StoreRefusingTheWriterPausesThePassWithItsReason()
    {
        await using var harness = await StartAsync();
        harness.Store!.FailNext("pages/", new PageStoreConfigurationException(
            "PUT /lodb-pages-test/pages/1/en/16.7.1/champions answered 403 (AccessDenied).",
            HttpStatusCode.Forbidden));
        var passId = await harness.LaunchAsync(Scope());

        await harness.RunAsync();
        var paused = await harness.PassAsync(passId);
        await harness.Get<PassControl>().ResumeAsync(passId, Token);

        Assert.Contains("AccessDenied", paused.PauseReason, StringComparison.Ordinal);
        Assert.NotNull(paused.PausedAt);
        var resumed = await harness.PassAsync(passId);
        Assert.Equal((null, null), (resumed.PausedAt, resumed.PauseReason));
    }

    [Fact]
    public async Task FailedPageLeavesItsReasonOnTheUnit()
    {
        await using var harness = await StartAsync();
        harness.Store!.FailNext("pages/", new PageStoreRefusedException(
            "The metadata of pages/1/en/16.7.1/champions weigh 2100 bytes, over 2048."));
        var passId = await harness.LaunchAsync(Scope());

        await harness.RunAsync();

        var unit = UnitOf(await harness.PassAsync(passId), Version);
        Assert.Equal((PrerenderUnitState.Completed, 1), (unit.State, unit.PagesFailed));
        Assert.Equal(
            "The metadata of pages/1/en/16.7.1/champions weigh 2100 bytes, over 2048.",
            unit.Failure);
    }

    [Fact]
    public async Task UnitFailedForGoodSaysWhy()
    {
        await using var harness = await StartAsync();
        harness.Catalog.Pages[Version] = FakeCatalog.Without(CatalogLoadStatus.Unknown);
        harness.Catalog.Pinned.Insert(0, Older);
        harness.Catalog.Pages[Older] = FakeCatalog.Without(CatalogLoadStatus.Pending);
        var passId = await harness.LaunchAsync(new PassScope { Locales = [UiLocale.En] });

        await harness.RunAsync();
        harness.Clock.Advance(UnitWaits.Longest);
        await harness.RunAsync();

        var pass = await harness.PassAsync(passId);
        var unknown = UnitOf(pass, Version);
        Assert.Equal(
            (PrerenderUnitState.Failed, "Data Dragon does not list this version."),
            (unknown.State, unknown.Failure));
        Assert.NotNull(unknown.EndedAt);
        var abandoned = UnitOf(pass, Older);
        Assert.Equal(PrerenderUnitState.Failed, abandoned.State);
        Assert.StartsWith("Gave up after 30 minutes:", abandoned.Failure, StringComparison.Ordinal);
        Assert.NotNull(abandoned.EndedAt);
    }

    private static PrerenderUnit UnitOf(PrerenderPass pass, PatchVersion version) =>
        pass.Units.Single(unit => unit.Version == version.Value);

    private static string PathOf(PatchVersion version, string page) =>
        $"/en/{version.Value}/{page}";

    private static PassScope Scope() => PassScope.Of(Version) with { Locales = [UiLocale.En] };

    private static ClaimedUnit Claimed(long passId) => new()
    {
        Pass = new RunningPass(passId, 1, Scope()),
        Version = Version,
        Locale = UiLocale.En,
    };

    private async Task<PrerenderHarness> StartAsync()
    {
        var harness = await PrerenderHarness.StartAsync(postgres);
        harness.Catalog.Pinned.Add(Version);
        return harness;
    }
}
