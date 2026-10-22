using LoDb.Api.Modules.Admin.Prerender.Promotion;
using LoDb.Api.Modules.Admin.Prerender.Passes;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Persistence.Prerender;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using LoDb.Ingestion.Catalog.States.Snapshots;
using LoDb.Ingestion.Pipeline.Versions;
using LoDb.Testing;
using LoDb.Testing.States;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.Prerender.Revision;

/// <summary>
/// The producer of the queue of the closed states: at a promotion, every version of each state
/// it closed, the one it made an older patch included, in the 21 locales, due once the servers
/// settle, for the served epoch and the active pass's; once each, through its watermark,
/// whoever calls it; nothing while the index lags behind the promotion; a new generation of the
/// index is history, but for its last promotion.
/// </summary>
public sealed class RevisionProducerTests(PostgresContainerFixture postgres)
{
    private static readonly DateTimeOffset ClosedByPromotion = RevisionRig.BuiltAt.AddHours(1);

    // The units of the patch a promotion replaced, as in production.
    private static readonly Dictionary<string, string?> Automatic = new()
    {
        ["LoDb:Prerender:AutoEnqueueOnPromotion"] = "true",
    };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PromotionEnqueuesEveryVersionOfEachStateItClosedInEveryLocale()
    {
        await using var rig = await RevisionRig.StartAsync(postgres);
        await rig.SettleAsync();
        rig.Index(1, RevisionRig.Closed("16.8.1", "16.10.1", ClosedByPromotion));
        var now = rig.Harness.Clock.GetUtcNow();

        await Listener(rig).OnPromotedAsync(RevisionRig.Latest, Token);

        var rows = await rig.RowsAsync();
        Assert.Equal(3 * UiLocales.All.Count, rows.Count);
        Assert.Equal(
            ["16.10.1", "16.8.1", "16.9.1"],
            rows.Select(static row => row.Version).Distinct().Order(StringComparer.Ordinal));
        Assert.Contains(rows, static row => row.Path == "/zh-hant/16.9.1/items/1036-long-sword");
        Assert.All(rows, row =>
        {
            Assert.Equal(
                (1, PrerenderDemandState.Pending, 0),
                (row.Epoch, row.State, row.Attempts));
            Assert.InRange(row.DueAt, now + TimeSpan.FromMinutes(21), now.AddMinutes(22));
        });
        Assert.Contains(
            rig.Harness.Logs.GetSnapshot(),
            static record => record.Id.Name == "prerender.revision.enqueued");
    }

    [Fact]
    public async Task ClosureIsEnqueuedOnceWhoeverAsksAgain()
    {
        await using var rig = await RevisionRig.StartAsync(postgres);
        await rig.SettleAsync();
        rig.Index(1, RevisionRig.Closed("16.9.1", "16.10.1", ClosedByPromotion));

        await Listener(rig).OnPromotedAsync(RevisionRig.Latest, Token);
        var first = (await rig.RowsAsync()).Count;
        await rig.Harness.Get<PrerenderReconciler>().ReconcileAsync(Token);
        var again = await rig.ProduceAsync();
        var rows = await rig.RowsAsync();

        Assert.Equal((2 * UiLocales.All.Count, 0), (first, again));
        Assert.Equal(2 * UiLocales.All.Count, rows.Count);
        var mark = await rig.WatermarkAsync();
        Assert.Equal((1L, ClosedByPromotion), (mark!.GenerationId, mark.ClosedThrough));
    }

    [Fact]
    public async Task ActivePassOfAnotherEpochGetsThePagesTooWhateverItsScope()
    {
        await using var rig = await RevisionRig.StartAsync(postgres);
        await rig.SettleAsync();
        await rig.Harness.OpenAsync(new PassLaunch
        {
            Epoch = 2,
            Scope = PassScope.Of(RevisionRig.Older) with { PageTypes = [ResourceType.Runes] },
        });
        rig.Index(1, RevisionRig.Closed("16.9.1", "16.10.1", ClosedByPromotion));

        await rig.ProduceAsync();

        var rows = await rig.RowsAsync();
        Assert.Equal(
            [(1, 2 * UiLocales.All.Count), (2, 2 * UiLocales.All.Count)],
            rows.GroupBy(static row => row.Epoch)
                .Select(static epoch => (epoch.Key, epoch.Count())));
    }

    // Epoch 2, filled by a completed pass, waits for its switch; epoch 3's pass was cancelled.
    [Fact]
    public async Task EpochOnStandbyForItsSwitchGetsThePagesToo()
    {
        await using var rig = await RevisionRig.StartAsync(postgres);
        await rig.SettleAsync();
        await AddPassAsync(rig, 2, PrerenderPassState.Completed);
        await AddPassAsync(rig, 3, PrerenderPassState.Cancelled);
        rig.Index(1, RevisionRig.Closed("16.9.1", "16.10.1", ClosedByPromotion));

        await rig.ProduceAsync();

        var rows = await rig.RowsAsync();
        Assert.Equal(
            [(1, 2 * UiLocales.All.Count), (2, 2 * UiLocales.All.Count)],
            rows.GroupBy(static row => row.Epoch)
                .Select(static epoch => (epoch.Key, epoch.Count())));
    }

    [Fact]
    public async Task ReconciliationEnqueuesTheClosedStatesWithoutAutomaticUnits()
    {
        await using var rig = await RevisionRig.StartAsync(postgres);
        await rig.SettleAsync();
        rig.Index(1, RevisionRig.Closed("16.9.1", "16.10.1", ClosedByPromotion));

        var units = await rig.Harness.Get<PrerenderReconciler>().ReconcileAsync(Token);

        Assert.Equal(0, units);
        Assert.Equal(2 * UiLocales.All.Count, (await rig.RowsAsync()).Count);
    }

    [Fact]
    public async Task NewGenerationIsHistoryButForWhatItsLastPromotionClosedAfterIt()
    {
        await using var rig = await RevisionRig.StartAsync(postgres);
        await rig.SettleAsync();
        var history = RevisionRig.Closed("16.8.1", "16.8.1", RevisionRig.BuiltAt);
        var rebuiltClosure = RevisionRig.Closed(
            "16.9.1",
            "16.10.1",
            RevisionRig.BuiltAt,
            new IndexedEntity(ResourceType.Items, "3078"));
        var closedAfter = RevisionRig.Closed(
            "16.9.1",
            "16.10.1",
            ClosedByPromotion,
            new IndexedEntity(ResourceType.Items, "3001"));
        rig.Index(2, history, rebuiltClosure, closedAfter);

        await rig.ProduceAsync();

        var rows = await rig.RowsAsync();
        Assert.All(rows, static row => Assert.EndsWith(
            "/items/3001-long-sword",
            row.Path,
            StringComparison.Ordinal));
        Assert.Equal(2 * UiLocales.All.Count, rows.Count);
        Assert.Equal(2L, (await rig.WatermarkAsync())!.GenerationId);
    }

    // The index lagged behind two promotions, then a rebuild walked past both: it dates their
    // closures alike, after its build. Those of the last promotion are enqueued all the same.
    [Fact]
    public async Task RebuildPastALaggingIndexEnqueuesWhatTheLastPromotionClosed()
    {
        await using var rig = await RevisionRig.StartAsync(postgres);
        await rig.SettleAsync();
        var walkedAt = RevisionRig.BuiltAt.AddSeconds(1);
        var earlier = RevisionRig.Closed(
            "16.8.1",
            "16.9.1",
            walkedAt,
            new IndexedEntity(ResourceType.Items, "3078"));
        var last = RevisionRig.Closed(
            "16.9.1",
            "16.10.1",
            walkedAt,
            new IndexedEntity(ResourceType.Items, "3001"));
        rig.Index(2, earlier, last);

        await rig.ProduceAsync();

        var rows = await rig.RowsAsync();
        Assert.All(rows, static row => Assert.EndsWith(
            "/items/3001-long-sword",
            row.Path,
            StringComparison.Ordinal));
        Assert.Equal(2 * UiLocales.All.Count, rows.Count);
    }

    [Fact]
    public async Task FirstRunTakesTheGenerationAsHistory()
    {
        await using var rig = await RevisionRig.StartAsync(postgres);
        rig.Index(1, RevisionRig.Closed("16.8.1", "16.9.1", RevisionRig.BuiltAt));

        var rows = await rig.ProduceAsync();

        Assert.Equal(0, rows);
        var mark = await rig.WatermarkAsync();
        Assert.Equal((1L, RevisionRig.BuiltAt), (mark!.GenerationId, mark.ClosedThrough));
        Assert.Contains(
            rig.Harness.Logs.GetSnapshot(),
            static record => record.Id.Name == "prerender.revision.generation");
    }

    [Fact]
    public async Task IndexThatDoesNotCoverThePromotionYetEnqueuesNothing()
    {
        await using var rig = await RevisionRig.StartAsync(postgres);
        rig.Harness.Index.Snapshot = TestStates.Snapshot(
            ["16.8.1", "16.9.1", "16.10.1"],
            [RevisionRig.Closed("16.8.1", "16.9.1", ClosedByPromotion)]);

        await Listener(rig).OnPromotedAsync(RevisionRig.Latest, Token);
        var reconciled = await rig.Harness.Get<PrerenderReconciler>().ReconcileAsync(Token);

        Assert.Equal(0, reconciled);
        Assert.Empty(await rig.RowsAsync());
        Assert.Null(await rig.WatermarkAsync());
        Assert.Contains(
            rig.Harness.Logs.GetSnapshot(),
            static record => record.Id.Name == "prerender.promotion.index_behind");
    }

    // Behind at the promotion, the index held open the state it closed: neither the unit of
    // 16.10.1 nor the queue started on it. Once it caught up, the reconciliation gives both,
    // and the queue takes 16.10.1 too, which a page of the demand may have stored meanwhile.
    [Fact]
    public async Task IndexCaughtUpLateGetsTheUnitAndEveryVersionOfTheClosedState()
    {
        await using var rig = await RevisionRig.StartAsync(postgres, Automatic);
        rig.Harness.Index.Snapshot = TestStates.Snapshot(
            ["16.8.1", "16.9.1", "16.10.1"],
            [TestStates.Open(RevisionRig.Item, "16.9.1")]);
        await rig.ProduceAsync();

        await Listener(rig).OnPromotedAsync(RevisionRig.Latest, Token);
        var whileBehind = (await rig.RowsAsync()).Count + await UnitsAsync(rig);
        rig.Index(1, RevisionRig.Closed("16.9.1", "16.10.1", ClosedByPromotion));
        var units = await rig.Harness.Get<PrerenderReconciler>().ReconcileAsync(Token);

        Assert.Equal((0, UiLocales.All.Count), (whileBehind, units));
        var rows = await rig.RowsAsync();
        Assert.Equal(
            ["16.10.1", "16.9.1"],
            rows.Select(static row => row.Version).Distinct().Order(StringComparer.Ordinal));
        Assert.Contains(rows, static row => row.Path == "/fr/16.10.1/items/1036-long-sword");
    }

    [Fact]
    public async Task CopyOfAnOlderGenerationThanTheWatermarkMovesNothing()
    {
        await using var rig = await RevisionRig.StartAsync(postgres);
        rig.Index(3);
        await rig.ProduceAsync();
        rig.Index(2, RevisionRig.Closed("16.9.1", "16.10.1", ClosedByPromotion));

        var rows = await rig.ProduceAsync();

        Assert.Equal(0, rows);
        Assert.Equal(3L, (await rig.WatermarkAsync())!.GenerationId);
    }

    [Fact]
    public async Task NoIndexYetEnqueuesNothing()
    {
        await using var rig = await RevisionRig.StartAsync(postgres);
        rig.Harness.Index.Snapshot = EntityStateSnapshot.Empty;

        Assert.Equal(0, await rig.ProduceAsync());
        Assert.Null(await rig.WatermarkAsync());
    }

    private static async Task AddPassAsync(RevisionRig rig, int epoch, PrerenderPassState state)
    {
        var finishedAt = rig.Harness.Clock.GetUtcNow();
        await using var db = rig.Harness.Database.CreateContext();
        db.PrerenderPasses.Add(new PrerenderPass
        {
            Epoch = epoch,
            State = state,
            CreatedAt = finishedAt.AddHours(-1),
            FinishedAt = finishedAt,
        });
        await db.SaveChangesAsync(Token);
    }

    private static async Task<int> UnitsAsync(RevisionRig rig)
    {
        await using var db = rig.Harness.Database.CreateContext();
        return await db.PrerenderUnits.CountAsync(Token);
    }

    private static IPromotionListener Listener(RevisionRig rig) =>
        Assert.Single(
            rig.Harness.Get<IEnumerable<IPromotionListener>>(),
            static listener => listener is PrerenderPromotionListener);
}
