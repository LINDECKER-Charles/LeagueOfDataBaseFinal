using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.States.Storage;
using LoDb.Ingestion.Catalog.States.Updates;
using LoDb.Testing;
using static LoDb.Ingestion.Tests.Catalog.States.Building.GenerationTests;

namespace LoDb.Ingestion.Tests.Catalog.States.Building;

/// <summary>
/// The generations a build stopped before making them current: one with the same coverage
/// start resumes where it stopped, any other goes, and a rebuild starts afresh.
/// </summary>
public sealed class PendingGenerationTests(PostgresContainerFixture postgres)
{
    private const string Missing = "90.2.1";

    private static readonly string[] Versions = ["90.1.1", "90.2.1", "90.3.1", "90.4.1", "90.5.1"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task BuildResumesTheGenerationAStoppedBuildLeft()
    {
        await using var index = await StateIndexHarness.CreateAsync(postgres, Versions);
        await StoreHistoryAsync(index);
        await index.PromoteAsync("90.3.1");
        var stopped = await index.BuildAsync();
        await index.SetAsideCurrentAsync();
        await index.PromoteAsync("90.5.1");

        var resumed = await index.BuildAsync();
        var coverage = await index.CoverageAsync();
        var states = await index.StatesAsync();
        await index.RebuildAsync();

        Assert.Equal(
            (StateIndexOutcome.Built, stopped.GenerationId, 2),
            (resumed.Outcome, resumed.GenerationId, resumed.VersionsIndexed));
        Assert.Equal(Versions, coverage);
        Assert.Equal(
            (await index.StatesAsync()).Select(Comparable),
            states.Select(Comparable));
    }

    [Fact]
    public async Task BuildDeletesWhatAStoppedBuildLeftFromAnotherStart()
    {
        await using var index = await StateIndexHarness.CreateAsync(postgres, Versions);
        var datasets = await ReducedDatasets.ReadAsync();
        foreach (var version in Versions.Where(static version => version != Missing))
        {
            await index.StoreAsync(version, datasets);
        }

        await index.PromoteAsync("90.5.1");
        var stopped = await index.BuildAsync();
        await index.SetAsideCurrentAsync();
        await index.StoreAsync(Missing, datasets);

        var built = await index.BuildAsync();

        Assert.Equal("90.3.1", stopped.CoverageStart?.Value);
        Assert.Equal(
            (StateIndexOutcome.Built, "90.1.1"),
            (built.Outcome, built.CoverageStart?.Value));
        Assert.Equal([built.GenerationId], await GenerationIdsAsync(index));
    }

    [Fact]
    public async Task RebuildDeletesWhatAStoppedBuildLeftAndStartsAfresh()
    {
        await using var index = await StateIndexHarness.CreateAsync(postgres, Versions);
        await StoreHistoryAsync(index);
        await index.PromoteAsync("90.5.1");
        var current = await index.BuildAsync();
        var pending = await index.Instance.Get<GenerationStore>()
            .CreateAsync(PatchVersion.Parse(Versions[0]), Token);

        var rebuilt = await index.RebuildAsync();

        Assert.Equal(StateIndexOutcome.Built, rebuilt.Outcome);
        Assert.NotEqual(current.GenerationId, rebuilt.GenerationId);
        Assert.NotEqual(pending.Id, rebuilt.GenerationId);
        Assert.Equal([rebuilt.GenerationId], await GenerationIdsAsync(index));
    }

    private static async Task<IReadOnlyList<long?>> GenerationIdsAsync(StateIndexHarness index) =>
        [.. (await index.GenerationsAsync()).Select(static generation => (long?)generation.Id)];
}
