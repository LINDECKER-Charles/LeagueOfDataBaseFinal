using LoDb.Domain.Catalog;
using LoDb.Domain.Paths;
using LoDb.Domain.States.Fields;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.States.Canonicals;
using LoDb.Ingestion.Catalog.States.Snapshots;
using LoDb.Testing.States;

namespace LoDb.Ingestion.Tests.Catalog.States.Canonicals;

/// <summary>
/// The single rule of the canonicals of the entity pages, one test per row of the table of
/// the spec (§ 3.3), over an index covering 8.7.1, 16.18.1 and 16.19.1 and the recorded en_US
/// catalog of 16.19.1, the latest version. Boots of Speed becomes Boots at 16.18.1; Garen
/// changes at 16.19.1; Abyssal Mask (3001) is Evenshroud at the latest version, unchanged in
/// the index; Molten Edge (3371) is retired at 16.18.1; Wukong is absent from 16.18.1.
/// </summary>
public sealed class StateViewTests
{
    private const string Archived = "8.7.1";
    private const string Previous = "16.18.1";
    private const string Latest = "16.19.1";

    private static readonly IndexedEntity Boots = new(ResourceType.Items, "1001");
    private static readonly IndexedEntity Mask = new(ResourceType.Items, "3001");
    private static readonly IndexedEntity MoltenEdge = new(ResourceType.Items, "3371");
    private static readonly IndexedEntity Garen = new(ResourceType.Champions, "Garen");
    private static readonly IndexedEntity Wukong = new(ResourceType.Champions, "MonkeyKing");
    private static readonly IndexedEntity Fiddlesticks =
        new(ResourceType.Champions, "FiddleSticks");

    private static readonly CanonicalPath BootsOfSpeed =
        CanonicalPath.Item("1001", "Boots of Speed");
    private static readonly CanonicalPath AbyssalMask = CanonicalPath.Item("3001", "Abyssal Mask");
    private static readonly CanonicalPath Molten = CanonicalPath.Item("3371", "Molten Edge");
    private static readonly CanonicalPath GarenPath = CanonicalPath.Champion("Garen");

    private static readonly EntityStateSnapshot Snapshot = TestStates.Snapshot(
        [Archived, Previous, Latest],
        [
            TestStates.Closed(Boots, Archived, Archived) with { Path = BootsOfSpeed.Value },
            TestStates.Open(Boots, Previous) with
            {
                Path = "items/1001-boots",
                PreviousOpener = V(Archived),
                ChangedFields = [FieldKey.Of(ChangeField.Name)],
            },
            TestStates.Open(Mask, Archived) with { Path = AbyssalMask.Value },
            TestStates.Closed(MoltenEdge, Archived, Archived) with { Path = Molten.Value },
            TestStates.Closed(Garen, Archived, Previous),
            TestStates.Open(Garen, Latest) with { PreviousOpener = V(Archived) },
            TestStates.Closed(Wukong, Archived, Archived),
            TestStates.Open(Wukong, Latest) with { PreviousOpener = V(Archived), IsReturn = true },
            TestStates.Open(Fiddlesticks, Archived),
        ]);

    [Fact]
    public async Task LatestPageIsItsOwnShortUrl()
    {
        var view = await ViewAsync(Snapshot);

        var page = view.Locate(CanonicalPath.Item("1001", "Boots"), V(Latest));

        Assert.Equal(CanonicalTarget.ShortUrl("items/1001-boots"), page.Canonical);
        Assert.True(page.IsLatest);
        Assert.True(page.IsPresentInLatest);
        Assert.Equal(V(Previous), page.State?.Opener);
        Assert.False(page.IsOpener);
    }

    [Fact]
    public async Task OpenerOfAClosedStateIsItsOwnCanonical()
    {
        var view = await ViewAsync(Snapshot);

        var page = view.Locate(BootsOfSpeed, V(Archived));

        Assert.Equal(CanonicalTarget.Pinned(V(Archived), BootsOfSpeed.Value), page.Canonical);
        Assert.True(page.IsOpener);
    }

    [Fact]
    public async Task OtherVersionOfAClosedStateNamesItsOpener()
    {
        var view = await ViewAsync(Snapshot);

        var page = view.Locate(GarenPath, V(Previous));

        Assert.Equal(CanonicalTarget.Pinned(V(Archived), GarenPath.Value), page.Canonical);
        Assert.False(page.IsOpener);
    }

    // The latest version renamed 3001 without the index seeing a change: the short URL
    // carries the latest slug, never the opener's.
    [Fact]
    public async Task OpenStateNamesTheShortUrlWithTheLatestPath()
    {
        var view = await ViewAsync(Snapshot);

        var page = view.Locate(AbyssalMask, V(Previous));

        Assert.Equal(CanonicalTarget.ShortUrl("items/3001-evenshroud"), page.Canonical);
        Assert.Equal(StateLocationKind.Covered, page.Location.Kind);
    }

    [Fact]
    public async Task VersionPastTheCoverageEndStandsInTheOpenState()
    {
        var lagging = TestStates.Snapshot([Archived], [TestStates.Open(Mask, Archived)]);
        var view = await ViewAsync(lagging);

        var page = view.Locate(AbyssalMask, V(Previous));

        Assert.Equal(StateLocationKind.Lagging, page.Location.Kind);
        Assert.Equal(CanonicalTarget.ShortUrl("items/3001-evenshroud"), page.Canonical);
    }

    [Fact]
    public async Task VersionOlderThanTheCoverageTakesTheResidualRule()
    {
        var view = await ViewAsync(Snapshot);

        var boots = view.Locate(BootsOfSpeed, V("7.22.1"));
        var molten = view.Locate(Molten, V("7.22.1"));

        Assert.Equal(StateLocationKind.Outside, boots.Location.Kind);
        Assert.Equal(CanonicalTarget.ShortUrl("items/1001-boots"), boots.Canonical);
        Assert.Equal(CanonicalTarget.Pinned(V("7.22.1"), Molten.Value), molten.Canonical);
        Assert.False(molten.IsPresentInLatest);
        Assert.Null(molten.State);
    }

    [Fact]
    public async Task VersionListedAfterTheLatestTakesTheResidualRule()
    {
        var view = await ViewAsync(Snapshot);

        var boots = view.Locate(BootsOfSpeed, V("16.20.1"));
        var molten = view.Locate(Molten, V("16.20.1"));

        Assert.Equal(CanonicalTarget.ShortUrl("items/1001-boots"), boots.Canonical);
        Assert.Equal(CanonicalTarget.Pinned(V("16.20.1"), Molten.Value), molten.Canonical);
    }

    [Fact]
    public void PageBeforeAnyPromotionIsItsOwnCanonical()
    {
        var view = new StateView(Snapshot, null, null);

        var page = view.Locate(BootsOfSpeed, V(Archived));

        Assert.Equal(CanonicalTarget.Pinned(V(Archived), BootsOfSpeed.Value), page.Canonical);
        Assert.Null(page.State);
        Assert.False(page.IsPresentInLatest);
    }

    [Fact]
    public async Task ChampionIdThatChangedItsCaseKeepsItsState()
    {
        var view = await ViewAsync(Snapshot);

        var page = view.Locate(CanonicalPath.Champion("Fiddlesticks"), V(Previous));

        Assert.Equal(V(Archived), page.State?.Opener);
        Assert.Equal(CanonicalTarget.ShortUrl("champions/Fiddlesticks"), page.Canonical);
    }

    [Fact]
    public async Task HistoryRunsUpToThePageStateNewestFirst()
    {
        var view = await ViewAsync(Snapshot);

        var latest = view.HistoryOf(view.Locate(CanonicalPath.Item("1001", "Boots"), V(Latest)));
        var pinned = view.HistoryOf(view.Locate(BootsOfSpeed, V(Archived)));
        var outside = view.HistoryOf(view.Locate(BootsOfSpeed, V("7.22.1")));

        Assert.Equal([V(Previous), V(Archived)], latest.Select(static state => state.Opener));
        Assert.Equal([V(Archived)], pinned.Select(static state => state.Opener));
        Assert.Empty(outside);
    }

    [Fact]
    public async Task ClosedStateEndsByAChangeOrARetirement()
    {
        var view = await ViewAsync(Snapshot);
        var states = Snapshot.StatesOf(Boots);

        var changed = view.SuccessorOf(states[0]);
        var retired = view.SuccessorOf(Snapshot.StatesOf(MoltenEdge)[0]);
        var absent = view.SuccessorOf(Snapshot.StatesOf(Wukong)[0]);

        Assert.Equal(new StateSuccessor(V(Previous), states[1]), changed);
        Assert.Equal(new StateSuccessor(V(Previous), null), retired);
        Assert.True(absent?.IsRetirement);
        Assert.Null(view.SuccessorOf(states[1]));
    }

    [Fact]
    public async Task ReturnIsComparedToTheStateBeforeTheAbsence()
    {
        var view = await ViewAsync(Snapshot);
        var states = Snapshot.StatesOf(Wukong);

        Assert.Equal(states[0], view.PreviousOf(states[1]));
        Assert.Null(view.PreviousOf(states[0]));
    }

    [Fact]
    public async Task OpenerLinkIsVersionedButAtTheLatestVersion()
    {
        var view = await ViewAsync(Snapshot);

        var boots = view.OpenerLinkOf(Snapshot.StatesOf(Boots)[1], BootsOfSpeed);
        var garen = view.OpenerLinkOf(Snapshot.StatesOf(Garen)[1], GarenPath);

        Assert.Equal(CanonicalTarget.Pinned(V(Previous), "items/1001-boots"), boots);
        Assert.Equal(CanonicalTarget.ShortUrl(GarenPath.Value), garen);
    }

    private static PatchVersion V(string version) => PatchVersion.Parse(version);

    private static async Task<StateView> ViewAsync(EntityStateSnapshot snapshot) =>
        new(snapshot, V(Latest), await CatalogFixtures.LatestAsync(CatalogFixtures.English));
}
