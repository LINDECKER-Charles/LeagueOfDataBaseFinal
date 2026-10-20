using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Passes;
using LoDb.Api.Modules.Admin.Prerender.Units;
using LoDb.Api.Tests.Prerender.Support;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.Prerender;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.Prerender;

/// <summary>
/// A pass resumes where it stopped, across restarts; a pause or a cancel is seen at the next
/// page; only one pass is active at a time; a version that cannot be rendered yet waits
/// without holding the others back, a while.
/// </summary>
public sealed class PassLifecycleTests(PostgresContainerFixture postgres)
{
    private static readonly PatchVersion Version = PatchVersion.Parse("16.7.1");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PausedPassResumesAtItsCursorWithoutRenderingAPageTwice()
    {
        await using var harness = await StartAsync();
        var passId = await harness.LaunchAsync(Scope());
        harness.Ssr.OnPage = async path =>
        {
            if (path.EndsWith("/runes", StringComparison.Ordinal))
            {
                await harness.Get<PassControl>().PauseAsync(passId, Token);
            }
        };

        await harness.RunAsync();
        var paused = await harness.PassAsync(passId);
        harness.Ssr.OnPage = null;
        await harness.Get<PassControl>().ResumeAsync(passId, Token);
        await harness.RunAsync();

        Assert.Equal(PrerenderPassState.Paused, paused.State);
        Assert.Equal((3, 3L), (Assert.Single(paused.Units).Cursor, paused.PagesWritten));
        Assert.Equal(FakeCatalog.SixPages.Select(PathOf), harness.Ssr.Pages);
        var resumed = await harness.PassAsync(passId);
        Assert.Equal((PrerenderPassState.Completed, 6L), (resumed.State, resumed.PagesWritten));
    }

    [Fact]
    public async Task UnitAProcessLeftRunningResumesAtItsCursor()
    {
        await using var harness = await StartAsync();
        var passId = await harness.LaunchAsync(Scope());
        await using (var db = harness.Database.CreateContext())
        {
            await db.PrerenderUnits.ExecuteUpdateAsync(
                static setters => setters
                    .SetProperty(static unit => unit.State, PrerenderUnitState.Running)
                    .SetProperty(static unit => unit.PagesTotal, 6)
                    .SetProperty(static unit => unit.PagesWritten, 4)
                    .SetProperty(static unit => unit.Cursor, 4),
                Token);
        }

        await harness.RunAsync();

        Assert.Equal(FakeCatalog.SixPages.Skip(4).Select(PathOf), harness.Ssr.Pages);
        var unit = Assert.Single((await harness.PassAsync(passId)).Units);
        Assert.Equal(
            (PrerenderUnitState.Completed, 6, 6),
            (unit.State, unit.PagesWritten, unit.Cursor));
    }

    [Fact]
    public async Task CancelledPassStopsAtTheNextPageAndLeavesItsUnitsAsTheyAre()
    {
        await using var harness = await StartAsync();
        var passId = await harness.LaunchAsync(Scope());
        harness.Ssr.OnPage = async path =>
        {
            if (path.EndsWith("/items", StringComparison.Ordinal))
            {
                await harness.Get<PassControl>().CancelAsync(passId, Token);
            }
        };

        await harness.RunAsync();
        await harness.RunAsync();

        var pass = await harness.PassAsync(passId);
        Assert.Equal(PrerenderPassState.Cancelled, pass.State);
        Assert.NotNull(pass.FinishedAt);
        var unit = Assert.Single(pass.Units);
        Assert.Equal((PrerenderUnitState.Running, 2), (unit.State, unit.Cursor));
        Assert.Equal(2, harness.Ssr.Pages.Count);
    }

    [Fact]
    public async Task SecondPassIsRefusedWhileOneIsActive()
    {
        await using var harness = await StartAsync();
        var launcher = harness.Get<PassLauncher>();
        var first = await harness.LaunchAsync(Scope());
        await harness.Get<PassControl>().PauseAsync(first, Token);

        var refused = await launcher.StartAsync(new PassLaunch { Epoch = 2 }, Token);
        await harness.Get<PassControl>().CancelAsync(first, Token);
        var second = await launcher.StartAsync(new PassLaunch { Epoch = 2 }, Token);

        Assert.Null(refused);
        Assert.NotNull(second);
        var resumed = await harness.Get<PassControl>().ResumeAsync(first, Token);
        Assert.Equal(PassChange.Refused, resumed);
    }

    [Fact]
    public async Task VersionDataDragonDoesNotListFailsItsUnitAndThePassGoesOn()
    {
        await using var harness = await StartAsync();
        var unknown = PatchVersion.Parse("16.6.1");
        harness.Catalog.Pinned.Add(unknown);
        harness.Catalog.Pages[unknown] = FakeCatalog.Without(CatalogLoadStatus.Unknown);
        var passId = await harness.LaunchAsync(new PassScope { Locales = [UiLocale.En] });

        await harness.RunAsync();

        var pass = await harness.PassAsync(passId);
        Assert.Equal(PrerenderPassState.Completed, pass.State);
        Assert.Equal(
            [PrerenderUnitState.Completed, PrerenderUnitState.Failed],
            pass.Units.OrderByDescending(static unit => PatchVersion.Parse(unit.Version))
                .Select(static unit => unit.State));
    }

    [Fact]
    public async Task VersionNotStoredYetWaitsWhileTheOthersCompleteThenFails()
    {
        await using var harness = await StartAsync();
        var waiting = PatchVersion.Parse("16.8.1");
        harness.Catalog.Pinned.Insert(0, waiting);
        harness.Catalog.Pages[waiting] = FakeCatalog.Without(CatalogLoadStatus.Pending);
        var passId = await harness.LaunchAsync(new PassScope { Locales = [UiLocale.En] });

        await harness.RunAsync();
        await harness.RunAsync();
        var meanwhile = await harness.PassAsync(passId);
        harness.Clock.Advance(UnitWaits.Longest);
        await harness.RunAsync();

        Assert.Equal(PrerenderPassState.Running, meanwhile.State);
        Assert.Equal(
            [PrerenderUnitState.Pending, PrerenderUnitState.Completed],
            UnitStates(meanwhile));
        Assert.Equal(FakeCatalog.SixPages.Select(PathOf), harness.Ssr.Pages);
        var pass = await harness.PassAsync(passId);
        Assert.Equal(PrerenderPassState.Completed, pass.State);
        Assert.Equal([PrerenderUnitState.Failed, PrerenderUnitState.Completed], UnitStates(pass));
        var warnings = harness.Logs.GetSnapshot()
            .Where(static record => record.Level == LogLevel.Warning
                && record.Category?.Contains("Prerender", StringComparison.Ordinal) == true)
            .Select(static record => record.Id.Name);
        Assert.Equal(["prerender.unit.waiting", "prerender.unit.abandoned"], warnings);
    }

    [Fact]
    public async Task VersionThatWaitsIsTriedAgainBetweenUnitsNotAfterThePass()
    {
        await using var harness = await PrerenderHarness.StartAsync(postgres);
        var (settling, first, last) = (Versions.Latest, Versions.Left, Versions.Older);
        harness.Catalog.Pinned.AddRange([settling, first, last]);
        var settled = harness.Clock.GetUtcNow() + TimeSpan.FromMinutes(1);
        harness.Catalog.PinnedFrom[settling] = settled;
        harness.Ssr.OnPage = path =>
        {
            if (path.Contains(first.Value, StringComparison.Ordinal))
            {
                harness.Clock.Advance(TimeSpan.FromSeconds(10));
            }

            return Task.CompletedTask;
        };
        var passId = await harness.LaunchAsync(new PassScope { Locales = [UiLocale.En] });

        await harness.RunAsync();

        var expected = new[] { first, settling, last }.SelectMany(static version =>
            FakeCatalog.SixPages.Select(page => $"/en/{version.Value}/{page}"));
        Assert.Equal(expected, harness.Ssr.Pages);
        Assert.Equal(PrerenderPassState.Completed, (await harness.PassAsync(passId)).State);
    }

    [Fact]
    public async Task VersionThatJustLeftTheLatestRankIsAnOlderPatchOnceEveryCacheLetsGo()
    {
        var promotedAt = DateTimeOffset.UnixEpoch;
        var reader = new VersionsReader(new CatalogVersions
        {
            Latest = Versions.Latest,
            LatestPromotedAt = promotedAt,
            Listed = [Versions.Latest, Versions.Left, Versions.Older],
            Ready = [Versions.Latest, Versions.Left],
        });
        var lifetime = TimeSpan.FromSeconds(45);
        var catalog = new CatalogPages(
            reader,
            Options.Create(new CatalogOptions { VersionsLifetime = lifetime }));

        async Task<DateTimeOffset?> From(string version) =>
            await catalog.PinnedFromAsync(PatchVersion.Parse(version), Token);

        Assert.Equal(promotedAt + (2 * lifetime) + TimeSpan.FromMinutes(1), await From("16.18.1"));
        Assert.Equal(DateTimeOffset.MinValue, await From("16.17.1"));
        Assert.Null(await From("16.19.1"));
        Assert.Null(await From("16.16.1"));
    }

    [Fact]
    public void RunningTimeLeavesThePausesOut()
    {
        var started = DateTimeOffset.UnixEpoch;
        var pass = new PrerenderPass
        {
            State = PrerenderPassState.Running,
            RunningSince = started,
        };

        PassTimes.Move(pass, PrerenderPassState.Paused, started.AddMinutes(10));
        PassTimes.Move(pass, PrerenderPassState.Running, started.AddHours(5));
        PassTimes.Move(pass, PrerenderPassState.Completed, started.AddHours(5).AddMinutes(20));

        Assert.Equal(1800, pass.ActiveSeconds);
        Assert.Equal((PrerenderPassState.Completed, null), (pass.State, pass.RunningSince));
        Assert.Equal(started.AddHours(5).AddMinutes(20), pass.FinishedAt);
        Assert.False(PassTimes.CanMove(PrerenderPassState.Completed, PrerenderPassState.Running));
    }

    private static string PathOf(string page) => $"/en/{Version.Value}/{page}";

    private static PrerenderUnitState[] UnitStates(PrerenderPass pass) =>
    [
        .. pass.Units.OrderByDescending(static unit => PatchVersion.Parse(unit.Version))
            .Select(static unit => unit.State),
    ];

    private static PassScope Scope() => PassScope.Of(Version) with { Locales = [UiLocale.En] };

    private async Task<PrerenderHarness> StartAsync()
    {
        var harness = await PrerenderHarness.StartAsync(postgres);
        harness.Catalog.Pinned.Add(Version);
        return harness;
    }

    private static class Versions
    {
        public static readonly PatchVersion Latest = PatchVersion.Parse("16.19.1");
        public static readonly PatchVersion Left = PatchVersion.Parse("16.18.1");
        public static readonly PatchVersion Older = PatchVersion.Parse("16.17.1");
    }

    // The versions as the API's cache holds them; the pass reads nothing else from it here.
    private sealed class VersionsReader(CatalogVersions versions) : ICatalogReader
    {
        public Task<CatalogVersions> GetVersionsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(versions);

        public Task<PatchVersion?> GetLatestAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<DateTimeOffset?> GetLatestPromotedAtAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<DdragonLanguage>> GetLanguagesAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CatalogLoad> GetAsync(
            PatchVersion version,
            DdragonLanguage language,
            ColdDemand demand,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
