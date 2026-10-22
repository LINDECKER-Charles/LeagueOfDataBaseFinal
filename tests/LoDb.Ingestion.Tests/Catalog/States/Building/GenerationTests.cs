using System.Net;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Persistence.States;
using LoDb.Ingestion.Catalog.States.Updates;
using LoDb.Ingestion.Egress.Errors;
using LoDb.Ingestion.Normalization;
using LoDb.Ingestion.Pipeline.Datasets;
using LoDb.Ingestion.Tests.Ddragon;
using LoDb.Testing;
using LoDb.Testing.Fixtures;
using static LoDb.Ingestion.Tests.Catalog.States.Building.ReducedDatasets;

namespace LoDb.Ingestion.Tests.Catalog.States.Building;

/// <summary>
/// The generations of the index: a promotion extends the current one version by version, a
/// build catches up or does nothing, and a rebuild writes a new one that reads the same,
/// closing dates included, before it takes the old one's place. The reconciliation builds
/// the first one; a list of versions that cannot be read leaves the index as it is.
/// </summary>
public sealed class GenerationTests(PostgresContainerFixture postgres)
{
    private const string VersionList = "/api/versions.json";

    private static readonly string[] Versions = ["90.1.1", "90.2.1", "90.3.1", "90.4.1", "90.5.1"];

    private static readonly StateIndexRequest Build = new() { Trigger = StateIndexTrigger.Build };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // 90.3.1 is never promoted: the promotion of 90.4.1 covers it all the same.
    [Fact]
    public async Task PromotionsAndARebuildGiveTheSameIndex()
    {
        await using var index = await StateIndexHarness.CreateAsync(postgres, Versions);
        await StoreHistoryAsync(index);
        foreach (var version in new[] { "90.1.1", "90.2.1", "90.4.1", "90.5.1" })
        {
            await index.PromoteAndListenAsync(version);
        }

        var promoted = await index.StatesAsync();
        var report = await index.RebuildAsync();
        var rebuilt = await index.StatesAsync();

        Assert.Equal(StateIndexOutcome.Built, report.Outcome);
        Assert.Equal(Versions, await index.CoverageAsync());
        Assert.Equal(promoted.Select(Comparable), rebuilt.Select(Comparable));
        Assert.Equal(
            promoted.Select(static state => state.ClosedAt),
            rebuilt.Select(static state => state.ClosedAt));
        Assert.Equal(
            [report.GenerationId],
            (await index.GenerationsAsync()).Select(static generation => (long?)generation.Id));
    }

    [Fact]
    public async Task BuildDoesNothingOnceTheIndexCoversTheLatestVersion()
    {
        await using var index = await StateIndexHarness.CreateAsync(postgres, Versions);
        await StoreHistoryAsync(index);
        await index.PromoteAsync("90.5.1");
        var built = await index.BuildAsync();

        var again = await index.BuildAsync();

        Assert.Equal(
            (StateIndexOutcome.Unchanged, built.GenerationId, 0),
            (again.Outcome, again.GenerationId, again.VersionsIndexed));
        Assert.Equal("90.1.1", again.CoverageStart?.Value);
    }

    [Fact]
    public async Task BuildExtendsTheCurrentGenerationToALaterPromotion()
    {
        await using var index = await StateIndexHarness.CreateAsync(postgres, Versions);
        await StoreHistoryAsync(index);
        await index.PromoteAsync("90.3.1");
        var built = await index.BuildAsync();
        await index.PromoteAsync("90.5.1");

        var extended = await index.BuildAsync();

        Assert.Equal(
            (StateIndexOutcome.Extended, built.GenerationId, 2, "90.5.1"),
            (extended.Outcome, extended.GenerationId, extended.VersionsIndexed,
                extended.CoverageEnd?.Value));
    }

    // A listed version whose en_US datasets are not stored ends the coverage; once stored, the
    // coverage starts earlier, in a new generation.
    [Fact]
    public async Task CoverageStartsAfterAMissingVersionThenMovesBackOnceStored()
    {
        await using var index = await StateIndexHarness.CreateAsync(postgres, Versions);
        var datasets = await ReadAsync();
        foreach (var version in Versions.Where(static version => version != "90.2.1"))
        {
            await index.StoreAsync(version, datasets);
        }

        await index.PromoteAsync("90.5.1");
        var gap = await index.BuildAsync();
        await index.StoreAsync("90.2.1", datasets);
        var stored = await index.BuildAsync();

        Assert.Equal("90.3.1", gap.CoverageStart?.Value);
        Assert.Equal(
            (StateIndexOutcome.Built, "90.1.1"),
            (stored.Outcome, stored.CoverageStart?.Value));
        Assert.NotEqual(gap.GenerationId, stored.GenerationId);
        Assert.Equal(Versions, await index.CoverageAsync());
    }

    // The promotion runs where Data Dragon is reachable: the en_US datasets of a version it
    // covers and the store lacks are ingested first (the recording's two complete versions).
    [Fact]
    public async Task PromotionIngestsTheEnglishDatasetsTheStoreLacks()
    {
        var previous = DdragonFixtures.Previous;
        var latest = DdragonFixtures.Latest;
        await using var index = await StateIndexHarness.CreateAsync(
            postgres, previous.Value, latest.Value);
        using (var replay = ReplayHarness.Create())
        {
            await index.StoreAsync(
                previous.Value,
                await CatalogFixtures.DatasetsAsync(replay, previous, DdragonLanguage.EnUs));
        }

        await index.PromoteAndListenAsync(previous.Value);
        await index.PromoteAndListenAsync(latest.Value);

        Assert.Equal([previous.Value, latest.Value], await index.CoverageAsync());
        var scope = new DatasetScope { Version = latest, Language = DdragonLanguage.EnUs };
        Assert.True(await index.Instance.Get<StoredDatasets>().ExistAsync(scope, Token));
    }

    // An API started on no index: its reconciliation builds it, as the command would.
    [Fact]
    public async Task ReconciliationBuildsTheIndexWhenNoneIsCurrent()
    {
        await using var index = await StateIndexHarness.CreateAsync(postgres, Versions);
        await StoreHistoryAsync(index);
        await index.PromoteAsync("90.5.1");

        var report = await index.UpdateAsync(new StateIndexRequest
        {
            Trigger = StateIndexTrigger.Reconcile,
        });

        Assert.Equal(
            (StateIndexOutcome.Built, Versions.Length),
            (report.Outcome, report.VersionsIndexed));
        Assert.Equal(Versions, await index.CoverageAsync());
    }

    // The build command runs in a process of its own, where no list is cached yet: the update
    // fails without touching the index, which tells whether one is current all the same.
    [Fact]
    public async Task UnreadableVersionListFailsTheUpdateAndLeavesTheIndexAsItIs()
    {
        await using var index = await StateIndexHarness.CreateAsync(postgres, Versions);
        await StoreHistoryAsync(index);
        await index.PromoteAsync("90.3.1");
        var command = index.StartInstance();
        command.Upstream.FailNext(
            static url => url.AbsolutePath == VersionList,
            int.MaxValue,
            HttpStatusCode.ServiceUnavailable);
        var updater = command.Get<IStateIndexUpdater>();

        await Assert.ThrowsAnyAsync<EgressException>(() => updater.UpdateAsync(Build, Token));
        var hadNone = !await updater.HasCurrentAsync(Token);
        await index.BuildAsync();
        await index.PromoteAsync("90.5.1");
        await Assert.ThrowsAnyAsync<EgressException>(() => updater.UpdateAsync(Build, Token));

        Assert.True(hadNone);
        Assert.True(await updater.HasCurrentAsync(Token));
        Assert.Equal(Versions[..3], await index.CoverageAsync());
    }

    /// <summary>
    /// Changes, a retirement, a return and an unchanged version, over five versions.
    /// </summary>
    internal static async Task StoreHistoryAsync(StateIndexHarness index)
    {
        var datasets = await ReadAsync();
        var dearer = datasets.ChangingItem(
            Trinity,
            static item => item with { Gold = item.Gold with { Total = item.Gold.Total + 100 } });
        await index.StoreAsync("90.1.1", datasets);
        await index.StoreAsync("90.2.1", dearer.WithoutItem(Potion));
        await index.StoreAsync("90.3.1", dearer);
        await index.StoreAsync("90.4.1", dearer.WithoutItem(Boots));
        await index.StoreAsync("90.5.1", dearer.WithoutItem(Boots));
    }

    /// <summary>A state as a rebuild must find it again, its closing date aside.</summary>
    internal static string Comparable(CatalogEntityState state) =>
        string.Join(
            '|',
            state.Resource,
            state.EntityKey,
            state.OpenerVersion,
            state.LastVersion,
            state.Fingerprint,
            state.Path,
            state.PreviousOpenerVersion,
            state.IsReturn,
            string.Join(',', state.ChangedFields));
}
