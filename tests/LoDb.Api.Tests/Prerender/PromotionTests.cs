using System.Globalization;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Passes;
using LoDb.Api.Modules.Admin.Prerender.Promotion;
using LoDb.Api.Tests.Prerender.Support;
using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.Ddragon;
using LoDb.Infrastructure.Persistence.Prerender;
using LoDb.Ingestion.Pipeline.Versions;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.Prerender;

/// <summary>
/// The patch that leaves the latest rank gets its units, one per locale: they join the active
/// pass ahead of the others, or open a pass on the served epoch; the reconciliation heals a
/// notice lost; nothing happens where the promotions are not to be followed (the staging).
/// </summary>
public sealed class PromotionTests(PostgresContainerFixture postgres)
{
    private const string Section = "LoDb:Prerender:";

    private static readonly PatchVersion Older = PatchVersion.Parse("16.17.1");
    private static readonly PatchVersion Left = PatchVersion.Parse("16.18.1");
    private static readonly PatchVersion Latest = PatchVersion.Parse("16.19.1");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PatchLeavingTheLatestRankJoinsTheActivePassAheadOfItsUnits()
    {
        await using var harness = await StartAsync(isAutomatic: true);
        harness.Catalog.Pinned.Add(Older);
        var passId = await harness.LaunchAsync(new PassScope { Locales = [UiLocale.En] });

        await Listener(harness).OnPromotedAsync(Latest, Token);
        await harness.RunAsync();

        var pass = await harness.PassAsync(passId);
        Assert.Equal(
            UiLocales.All.Count,
            pass.Units.Count(static unit => unit.Version == "16.18.1"));
        Assert.Equal(PrerenderPassState.Completed, pass.State);
        Assert.StartsWith("/ar/16.18.1/", harness.Ssr.Pages[0], StringComparison.Ordinal);
        Assert.StartsWith("/en/16.17.1/", harness.Ssr.Pages[^1], StringComparison.Ordinal);
        Assert.Contains(
            harness.Logs.GetSnapshot(),
            static record => record.Id.Name == "prerender.promotion.enqueued");
    }

    [Fact]
    public async Task WithoutAnActivePassThePatchOpensOneOnTheServedEpoch()
    {
        await using var harness = await StartAsync(isAutomatic: true, servedEpoch: 3);

        await Listener(harness).OnPromotedAsync(Latest, Token);

        await using var db = harness.Database.CreateContext();
        var pass = await db.PrerenderPasses.Include(static row => row.Units).SingleAsync(Token);
        Assert.Equal((3, PrerenderPassState.Running), (pass.Epoch, pass.State));
        // jsonb keeps its own spacing: the stored form is compared as the engine writes it.
        Assert.Equal(
            "{\"versions\":[\"16.18.1\"]}",
            PassScopeJson.Write(PassScopeJson.Read(pass.Scope)));
        Assert.Equal(
            UiLocales.All.Select(UiLocales.Code).Order(StringComparer.Ordinal),
            pass.Units.Select(static unit => unit.Locale).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task PatchIsRenderedOnlyOnceNoServerHoldsItForTheLatestOne()
    {
        await using var harness = await StartAsync(isAutomatic: true);
        var settled = harness.Clock.GetUtcNow() + TimeSpan.FromMinutes(1);
        harness.Catalog.PinnedFrom[Left] = settled;
        harness.Ssr.IsRedirected = _ => harness.Clock.GetUtcNow() < settled;

        await Listener(harness).OnPromotedAsync(Latest, Token);
        await harness.RunAsync();
        var early = harness.Ssr.Pages.Count;
        harness.Clock.Advance(TimeSpan.FromMinutes(1));
        await harness.RunAsync();

        Assert.Equal(0, early);
        await using var db = harness.Database.CreateContext();
        var pass = await db.PrerenderPasses.Include(static row => row.Units).SingleAsync(Token);
        Assert.Equal(PrerenderPassState.Completed, pass.State);
        Assert.All(
            pass.Units,
            static unit => Assert.Equal(PrerenderUnitState.Completed, unit.State));
        Assert.Equal(
            (UiLocales.All.Count * FakeCatalog.SixPages.Count, 0L),
            (pass.PagesWritten, pass.PagesSkipped));
    }

    [Fact]
    public async Task UnitLeftPendingInACompletedPassIsAddedAgain()
    {
        await using var harness = await StartAsync(isAutomatic: true);
        await using (var db = harness.Database.CreateContext())
        {
            var now = DateTimeOffset.UtcNow;
            var pass = new PrerenderPass
            {
                Epoch = 1,
                State = PrerenderPassState.Completed,
                Scope = PassScopeJson.Write(PassScope.Of(Left)),
                CreatedAt = now,
                FinishedAt = now,
            };
            pass.Units.Add(PassLauncher.RowOf(new PlannedUnit(Left, UiLocale.En, 0), now));
            db.PrerenderPasses.Add(pass);
            await db.SaveChangesAsync(Token);
        }

        var reconciled = await harness.Get<PrerenderReconciler>().ReconcileAsync(Token);

        Assert.Equal(UiLocales.All.Count, reconciled);
    }

    [Fact]
    public async Task NothingIsAddedWhereThePromotionsAreNotFollowed()
    {
        await using var harness = await StartAsync(isAutomatic: false);

        await Listener(harness).OnPromotedAsync(Latest, Token);
        var reconciled = await harness.Get<PrerenderReconciler>().ReconcileAsync(Token);

        Assert.Equal(0, reconciled);
        await using var db = harness.Database.CreateContext();
        Assert.False(await db.PrerenderPasses.AnyAsync(Token));
    }

    [Fact]
    public async Task ReconciliationHealsALostNoticeOnce()
    {
        await using var harness = await StartAsync(isAutomatic: true);
        var reconciler = harness.Get<PrerenderReconciler>();

        var healed = await reconciler.ReconcileAsync(Token);
        await harness.RunAsync();
        var again = await reconciler.ReconcileAsync(Token);

        Assert.Equal((UiLocales.All.Count, 0), (healed, again));
        Assert.All(
            harness.Ssr.Pages,
            static path => Assert.Contains("/16.18.1/", path, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ActivePassOfOneTypeOfPageMakesThePatchWaitForTheNextOne()
    {
        await using var harness = await StartAsync(isAutomatic: true);
        var typed = await harness.LaunchAsync(
            new PassScope { Locales = [UiLocale.En], PageTypes = [ResourceType.Items] });

        await Listener(harness).OnPromotedAsync(Latest, Token);
        var units = (await harness.PassAsync(typed)).Units.Count;
        await harness.Get<PassControl>().CancelAsync(typed, Token);
        var reconciled = await harness.Get<PrerenderReconciler>().ReconcileAsync(Token);

        Assert.Equal((1, UiLocales.All.Count), (units, reconciled));
        Assert.Contains(
            harness.Logs.GetSnapshot(),
            static record => record.Id.Name == "prerender.promotion.deferred");
    }

    private static IPromotionListener Listener(PrerenderHarness harness) =>
        Assert.Single(
            harness.Get<IEnumerable<IPromotionListener>>(),
            static listener => listener is PrerenderPromotionListener);

    // Two promotions: 16.18.1, then 16.19.1, which left 16.18.1 behind.
    private async Task<PrerenderHarness> StartAsync(bool isAutomatic, int servedEpoch = 1)
    {
        var harness = await PrerenderHarness.StartAsync(
            postgres,
            settings: new Dictionary<string, string?>
            {
                [Section + "AutoEnqueueOnPromotion"] = isAutomatic ? "true" : "false",
                [Section + "ServedEpoch"] = servedEpoch.ToString(CultureInfo.InvariantCulture),
            });
        harness.Catalog.Pinned.Add(Left);
        await using var db = harness.Database.CreateContext();
        db.DdragonVersions.AddRange(Promoted(Left, 2), Promoted(Latest, 1));
        await db.SaveChangesAsync(Token);
        return harness;
    }

    private static DdragonVersion Promoted(PatchVersion version, int daysAgo)
    {
        var promotedAt = DateTimeOffset.UtcNow.AddDays(-daysAgo);
        return new DdragonVersion
        {
            Version = version.Value,
            Status = DdragonVersionStatus.Ready,
            DiscoveredAt = promotedAt,
            UpdatedAt = promotedAt,
            ReadyAt = promotedAt,
            PromotedAt = promotedAt,
        };
    }
}
