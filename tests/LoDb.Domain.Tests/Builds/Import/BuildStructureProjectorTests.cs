using LoDb.Domain.Builds.Import;
using LoDb.Domain.Builds.Rules;
using LoDb.Domain.Builds.Structures;
using LoDb.Domain.Catalog.Modes;
using LoDb.Domain.Tests.Builds.Samples;
using LoDb.Domain.Tests.Catalog.Samples;

namespace LoDb.Domain.Tests.Builds.Import;

/// <summary>
/// An import to another patch, as the legacy projector tests state it: runes kept whole or
/// reset, items gone or off the map dropped and reported, the champion kept and flagged.
/// </summary>
public sealed class BuildStructureProjectorTests
{
    private static readonly BuildCatalog Target = new(
        ["Aatrox", "Ahri"],
        BuildSamples.Trees.Take(2),
        [.. BuildSamples.Items, ItemSamples.Named("771055", "Doran's Blade")]);

    [Fact]
    public void ACompatibleBuildCarriesOverUntouched()
    {
        var source = BuildSamples.Valid with
        {
            Steps = [BuildSamples.Step("Start", "go", "1055", "2003")],
        };

        var projection = Project(source, GameMode.SummonersRift);

        Assert.False(projection.Report.ChampionMissing);
        Assert.False(projection.Report.RunesReset);
        Assert.Empty(projection.Report.DroppedItems);
        Assert.Equal([8005, 9101, 9104, 8014], projection.Structure.Runes.PrimarySelections);
        Assert.Equal([8126, 8138], projection.Structure.Runes.SecondarySelections);
        Assert.Equal("go", projection.Structure.Steps[0].Note);
        Assert.Equal(["1055", "2003"], projection.Structure.Steps[0].Items);
    }

    [Theory]
    [InlineData("Nobody")]
    [InlineData("")]
    public void AMissingChampionIsFlaggedButItsIdIsKept(string championId)
    {
        var projection = Project(
            BuildSamples.Valid with { ChampionId = championId },
            GameMode.SummonersRift);

        Assert.True(projection.Report.ChampionMissing);
        Assert.Equal(championId, projection.Structure.ChampionId);
    }

    [Fact]
    public void ItemsGoneOrOffTheMapAreDroppedAndEmptiedStepsRemoved()
    {
        var source = BuildSamples.Valid with
        {
            Steps =
            [
                BuildSamples.Step("Core", null, "1055", "3006", "9999"),
                BuildSamples.Step("Boots only", null, "3006"),
            ],
        };

        // ARAM, map 12: the greaves are off the map, 9999 is on no patch.
        var projection = Project(source, GameMode.Aram);

        Assert.Equal(["1055"], Assert.Single(projection.Structure.Steps).Items);
        Assert.Equal(
            [
                new DroppedItem { Step = 0, Id = "3006", Name = "Berserker Greaves" },
                new DroppedItem { Step = 0, Id = "9999", Name = "9999" },
                new DroppedItem { Step = 1, Id = "3006", Name = "Berserker Greaves" },
            ],
            projection.Report.DroppedItems);
    }

    [Fact]
    public void AClassicItemIsDroppedUnderItsQualifiedName()
    {
        var source = BuildSamples.Valid with
        {
            Steps = [BuildSamples.Step("Start", null, "1055", "771055")],
        };

        var dropped = Project(source, GameMode.SummonersRift).Report.DroppedItems;

        Assert.Equal("Doran's Blade [771055]", Assert.Single(dropped).Name);
    }

    [Fact]
    public void StepsThatAreNoObjectAreSkippedButStillCounted()
    {
        var source = BuildSamples.Valid with
        {
            Steps = [null, BuildSamples.Step("Late", null, "9999", null)],
        };

        var projection = Project(source, GameMode.SummonersRift);

        Assert.Empty(projection.Structure.Steps);
        Assert.Equal(1, Assert.Single(projection.Report.DroppedItems).Step);
    }

    [Fact]
    public void RunesAreResetWhenAnIdIsGoneFromTheTargetTrees()
    {
        // 7777 is on no tree of the target patch.
        var source = BuildSamples.WithRunes(static runes => runes with
        {
            PrimarySelections = [8005, 9101, 9104, 7777],
        });

        var projection = Project(source, GameMode.SummonersRift);

        Assert.True(projection.Report.RunesReset);
        Assert.Same(RunePage.Blank, projection.Structure.Runes);
    }

    [Fact]
    public void RunesOfATreeTheTargetLacksAreReset()
    {
        var source = BuildSamples.WithRunes(static runes => runes with
        {
            SecondaryStyleId = BuildSamples.Sorcery,
            SecondarySelections = [8224, 8210],
        });

        Assert.True(Project(source, GameMode.SummonersRift).Report.RunesReset);
    }

    [Fact]
    public void APageNeverConfiguredIsBlankWithoutBeingReset()
    {
        var projection = Project(BuildSamples.Valid with { Runes = null }, GameMode.Aram);

        Assert.False(projection.Report.RunesReset);
        Assert.Same(RunePage.Blank, projection.Structure.Runes);
    }

    [Fact]
    public void APatchWithoutRunesResetsEveryPage()
    {
        var noRunes = new BuildCatalog(["Aatrox"], [], BuildSamples.Items);

        var projection = BuildStructureProjector.Project(
            BuildSamples.Valid,
            GameMode.SummonersRift,
            noRunes);

        Assert.True(projection.Report.RunesReset);
    }

    private static BuildProjection Project(BuildStructureInput source, GameMode mode) =>
        BuildStructureProjector.Project(source, mode, Target);
}
