using LoDb.Api.Modules.Admin.Prerender.Coverage;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.Prerender;

namespace LoDb.Api.Tests.Prerender.Monitoring;

/// <summary>
/// What an epoch holds of each (version, locale), folded from the units of its passes: each
/// type of page decided by the latest completed unit over it, a cell per (version, locale),
/// and the summary that says whether the epoch is ready.
/// </summary>
public sealed class CoverageFoldTests
{
    private const long EveryType = 1;
    private const long ItemsOnly = 2;
    private const long ChampionsOnly = 3;
    private const string Older = "16.1.1";
    private const string Newer = "16.2.1";

    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;

    private static readonly Dictionary<long, int> TypesByPass = new()
    {
        [EveryType] = TypeMask.All,
        [ItemsOnly] = TypeMask.Of(ResourceType.Items),
        [ChampionsOnly] = TypeMask.Of(ResourceType.Champions),
    };

    [Fact]
    public void EachCellSaysWhatItsUnitsHold()
    {
        var map = CoverageFold.Of(
            [
                Unit(EveryType, "en", PrerenderUnitState.Completed),
                Unit(EveryType, "fr", PrerenderUnitState.Completed) with { PagesFailed = 2 },
                Unit(ItemsOnly, "de", PrerenderUnitState.Completed),
                Unit(EveryType, "es", PrerenderUnitState.Failed),
                Unit(EveryType, "it", PrerenderUnitState.Running) with { EndedAt = null },
            ],
            TypesByPass);

        Assert.Equal(('c', TypeMask.All), CodeAndHeld(map, "en"));
        Assert.Equal(('w', TypeMask.None), CodeAndHeld(map, "fr"));
        Assert.Equal(2, map.CellOf(Older, "fr")!.FailedPages);
        Assert.Equal(('p', TypeMask.Of(ResourceType.Items)), CodeAndHeld(map, "de"));
        Assert.Equal(('f', TypeMask.None), CodeAndHeld(map, "es"));
        Assert.Equal(('p', TypeMask.None), CodeAndHeld(map, "it"));
        Assert.Null(map.CellOf(Older, "ja"));
        Assert.Equal(
            [(Older, "de"), (Older, "en")],
            map.HeldFor([ResourceType.Items]).Order());
    }

    // A unit cannot tell which type its pages failed on: they mark all its types, and only a
    // later unit over a type clears that one.
    [Fact]
    public void CleanPassOverTheItemsLeavesTheFailuresOnTheChampions()
    {
        var map = CoverageFold.Of(
            [
                Unit(EveryType, "en", PrerenderUnitState.Completed, minutes: 1) with
                {
                    PagesFailed = 3,
                    PagesWritten = 1_000,
                },
                Unit(ItemsOnly, "en", PrerenderUnitState.Completed, minutes: 2) with
                {
                    PagesWritten = 600,
                },
            ],
            TypesByPass);

        var cell = map.CellOf(Older, "en")!;
        Assert.Equal(('w', TypeMask.Of(ResourceType.Items)), (cell.Code, cell.Held));
        Assert.Equal((3, 1_000), (cell.FailedPages, cell.Pages));
        Assert.Equal(Start.AddMinutes(2), cell.LastEndedAt);
    }

    // A unit ended before ended_at was kept stands where its last change puts it.
    [Theory]
    [InlineData(1, 'c')]
    [InlineData(3, 'w')]
    public void LatestUnitOverATypeDecidesIt(int minutesOfTheOlderRow, char code)
    {
        var map = CoverageFold.Of(
            [
                Unit(EveryType, "en", PrerenderUnitState.Completed, minutes: 2),
                Unit(ChampionsOnly, "en", PrerenderUnitState.Completed) with
                {
                    PagesFailed = 1,
                    EndedAt = null,
                    UpdatedAt = Start.AddMinutes(minutesOfTheOlderRow),
                },
            ],
            TypesByPass);

        Assert.Equal(code, map.CellOf(Older, "en")!.Code);
    }

    [Fact]
    public void SummaryCountsWhatKeepsTheEpochFromBeingReady()
    {
        var units = UiLocales.All
            .Select(locale => Unit(EveryType, UiLocales.Code(locale), PrerenderUnitState.Completed)
                with { Version = Newer })
            .Concat(
            [
                Unit(EveryType, "en", PrerenderUnitState.Completed),
                Unit(EveryType, "fr", PrerenderUnitState.Completed) with { PagesFailed = 2 },
                Unit(ItemsOnly, "de", PrerenderUnitState.Completed),
                Unit(EveryType, "es", PrerenderUnitState.Failed),
            ]);

        var summary = CoverageFold.Of(units, TypesByPass)
            .Summary([PatchVersion.Parse(Newer), PatchVersion.Parse(Older)]);

        Assert.Equal(
            (2, 42, 23, 22, 1, 18, 2L, 1, 1),
            (summary.PinnedVersions, summary.ExpectedUnits, summary.CompleteUnits,
                summary.CleanUnits, summary.FailedUnits, summary.GapUnits, summary.FailedPages,
                summary.CompleteVersions, summary.GapVersions));
    }

    private static (char, int) CodeAndHeld(CoverageMap map, string locale)
    {
        var cell = map.CellOf(Older, locale)!;
        return (cell.Code, cell.Held);
    }

    private static CoverageUnit Unit(
        long passId,
        string locale,
        PrerenderUnitState state,
        int minutes = 1) => new()
        {
            PassId = passId,
            Version = Older,
            Locale = locale,
            State = state,
            PagesWritten = state == PrerenderUnitState.Failed ? 0 : 500,
            UpdatedAt = Start.AddMinutes(minutes),
            EndedAt = Start.AddMinutes(minutes),
        };
}
