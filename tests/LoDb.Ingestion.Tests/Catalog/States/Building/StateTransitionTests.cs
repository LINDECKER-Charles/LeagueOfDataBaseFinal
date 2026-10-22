using LoDb.Domain.Catalog;
using LoDb.Infrastructure.Persistence.States;
using LoDb.Ingestion.Catalog.States.Updates;
using LoDb.Testing;
using static LoDb.Ingestion.Tests.Catalog.States.Building.ReducedDatasets;

namespace LoDb.Ingestion.Tests.Catalog.States.Building;

/// <summary>
/// What a version does to the states of the index: an entity that keeps its fingerprint
/// extends its state, one that changes or disappears closes it, one that changes, is new or
/// comes back opens one; a resource whose dataset is absent counts for nothing.
/// </summary>
public sealed class StateTransitionTests(PostgresContainerFixture postgres)
{
    private const string First = "90.1.1";
    private const string Second = "90.2.1";
    private const string Third = "90.3.1";
    private const string Fourth = "90.4.1";

    [Fact]
    public async Task UnchangedEntitiesKeepOneOpenStateFromTheCoverageStart()
    {
        await using var index = await StateIndexHarness.CreateAsync(postgres, First, Second);
        var datasets = await ReadAsync();
        await index.StoreAsync(First, datasets);
        await index.StoreAsync(Second, datasets);
        await index.PromoteAsync(Second);

        var report = await index.BuildAsync();
        var states = await index.StatesAsync();

        Assert.Equal(StateIndexOutcome.Built, report.Outcome);
        Assert.Equal([First, Second], await index.CoverageAsync());
        Assert.Equal(
            Enum.GetValues<ResourceType>(),
            states.Select(static state => state.Resource).Distinct());
        Assert.All(states, static state => Assert.Equal(
            (First, null, null, false, 0),
            (state.OpenerVersion, state.LastVersion, state.PreviousOpenerVersion, state.IsReturn,
                state.ChangedFields.Count)));
        Assert.Equal("items/3078-trinity-force", Single(states, ResourceType.Items, Trinity).Path);
    }

    [Fact]
    public async Task ChangedEntityClosesItsStateAndOpensOneWithTheChangedFields()
    {
        await using var index = await StateIndexHarness.CreateAsync(postgres, First, Second);
        var datasets = await ReadAsync();
        await index.StoreAsync(First, datasets);
        await index.StoreAsync(Second, datasets.ChangingItem(
            Trinity,
            static item => item with { Gold = item.Gold with { Total = item.Gold.Total + 100 } }));
        await index.PromoteAsync(Second);

        await index.BuildAsync();
        var trinity = Of(await index.StatesAsync(), ResourceType.Items, Trinity);

        Assert.Equal(
            [(First, First, null, false), (Second, null, First, false)],
            trinity.Select(static state => (state.OpenerVersion, state.LastVersion,
                state.PreviousOpenerVersion, state.IsReturn)));
        Assert.NotNull(trinity[0].ClosedAt);
        Assert.Equal(["gold.total"], trinity[1].ChangedFields);
    }

    [Fact]
    public async Task RetiredEntityComesBackAsAReturnEvenUnchanged()
    {
        await using var index = await StateIndexHarness.CreateAsync(postgres, First, Second, Third);
        var datasets = await ReadAsync();
        await index.StoreAsync(First, datasets);
        await index.StoreAsync(Second, datasets.WithoutItem(Potion));
        await index.StoreAsync(Third, datasets);
        await index.PromoteAsync(Third);

        await index.BuildAsync();
        var potion = Of(await index.StatesAsync(), ResourceType.Items, Potion);

        Assert.Equal(
            [(First, First, null, false, 0), (Third, null, First, true, 0)],
            potion.Select(static state => (state.OpenerVersion, state.LastVersion,
                state.PreviousOpenerVersion, state.IsReturn, state.ChangedFields.Count)));
    }

    // 3001 was Abyssal Scepter, then Abyssal Mask, then Evenshroud: no rule of its own, the
    // state changes and its path follows the opener's name.
    [Fact]
    public async Task ReusedIdChangesStateUnderTheOpenersPath()
    {
        await using var index = await StateIndexHarness.CreateAsync(postgres, First, Second);
        var datasets = await ReadAsync();
        await index.StoreAsync(First, datasets.ChangingItem(
            Evenshroud,
            static item => item with { Name = "Abyssal Mask", Description = "Another item." }));
        await index.StoreAsync(Second, datasets);
        await index.PromoteAsync(Second);

        await index.BuildAsync();
        var item = Of(await index.StatesAsync(), ResourceType.Items, Evenshroud);

        Assert.Equal(
            ["items/3001-abyssal-mask", "items/3001-evenshroud"],
            item.Select(static state => state.Path));
        Assert.Contains("name", item[1].ChangedFields);
        Assert.Contains("description", item[1].ChangedFields);
    }

    // Data Dragon wrote FiddleSticks up to 3.x: the key folds the case, the path keeps the
    // opener's.
    [Fact]
    public async Task IdWhoseCaseChangesKeepsItsState()
    {
        await using var index = await StateIndexHarness.CreateAsync(postgres, First, Second);
        var datasets = await ReadAsync();
        await index.StoreAsync(First, datasets.RenamingChampion("Fiddlesticks", "FiddleSticks"));
        await index.StoreAsync(Second, datasets);
        await index.PromoteAsync(Second);

        await index.BuildAsync();
        var state = Single(await index.StatesAsync(), ResourceType.Champions, "fiddlesticks");

        Assert.Equal(
            (First, null, "champions/FiddleSticks"),
            (state.OpenerVersion, state.LastVersion, state.Path));
    }

    // The runes before 7.22.1: no state, then states neither extended nor interrupted by a
    // version without them.
    [Fact]
    public async Task ResourceWithoutDatasetNeitherExtendsNorInterruptsItsStates()
    {
        await using var index = await StateIndexHarness.CreateAsync(
            postgres, First, Second, Third, Fourth);
        var datasets = await ReadAsync();
        await index.StoreAsync(First, datasets.WithoutRunes());
        await index.StoreAsync(Second, datasets);
        await index.StoreAsync(Third, datasets.WithoutRunes());
        await index.StoreAsync(Fourth, datasets);
        await index.PromoteAsync(Fourth);

        await index.BuildAsync();
        var states = await index.StatesAsync();

        Assert.Equal([First, Second, Third, Fourth], await index.CoverageAsync());
        Assert.All(
            states.Where(static state => state.Resource == ResourceType.Runes),
            static state => Assert.Equal((Second, null), (state.OpenerVersion, state.LastVersion)));
    }

    // A version Data Dragon does not ship counts as unlisted.
    [Fact]
    public async Task VersionWithoutAnyDatasetIsNotCovered()
    {
        await using var index = await StateIndexHarness.CreateAsync(postgres, First, Second, Third);
        var datasets = await ReadAsync();
        await index.StoreAsync(First, datasets);
        await index.StoreAsync(Second, datasets.WithoutAnything());
        await index.StoreAsync(Third, datasets.WithoutItem(Potion));
        await index.PromoteAsync(Third);

        await index.BuildAsync();
        var potion = Single(await index.StatesAsync(), ResourceType.Items, Potion);

        Assert.Equal([First, Third], await index.CoverageAsync());
        Assert.Equal((First, First), (potion.OpenerVersion, potion.LastVersion));
    }

    private static List<CatalogEntityState> Of(
        IEnumerable<CatalogEntityState> states,
        ResourceType resource,
        string key) =>
        [.. states.Where(state => state.Resource == resource && state.EntityKey == key)];

    private static CatalogEntityState Single(
        IEnumerable<CatalogEntityState> states,
        ResourceType resource,
        string key) =>
        Assert.Single(Of(states, resource, key));
}
