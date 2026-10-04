using LoDb.Domain.Builds.Rules;
using LoDb.Domain.Builds.Structures;
using LoDb.Domain.Catalog.Modes;
using LoDb.Domain.Tests.Builds.Samples;
using LoDb.Domain.Tests.Catalog.Samples;

namespace LoDb.Domain.Tests.Builds.Rules;

/// <summary>
/// The check before a save, as the legacy gate tests state it: the validator's codes, then
/// the items the mode's map does not offer, named once each.
/// </summary>
public sealed class BuildCatalogGateTests
{
    private static readonly BuildCatalog WithClassic = new(
        ["Aatrox", "Ahri"],
        BuildSamples.Trees,
        [.. BuildSamples.Items, ItemSamples.Named("771055", "Doran's Blade")]);

    [Fact]
    public void AValidStructureOnItsModeYieldsNoError()
    {
        var verdict = Evaluate(Items("1055", "2003"), GameMode.SummonersRift);

        Assert.True(verdict.IsValid);
        Assert.Empty(verdict.UnavailableItems);
    }

    [Fact]
    public void AnItemWithoutMapFlagsStaysAvailableInEveryMode() =>
        Assert.True(Evaluate(Items("2003"), GameMode.Arena).IsValid);

    [Fact]
    public void ItemsOffTheMapAreNamedOnce()
    {
        var verdict = Evaluate(Items("1055", "3006", "3006"), GameMode.Aram);

        Assert.Equal([BuildErrors.ItemMode], verdict.Codes);
        Assert.Equal(["Berserker Greaves"], verdict.UnavailableItems);
    }

    [Fact]
    public void AnUnknownIdIsAPatchProblemNotAModeOne()
    {
        var structure = Items("9999") with { ChampionId = "Nobody" };

        var verdict = Evaluate(structure, GameMode.SummonersRift);

        Assert.Equal([BuildErrors.ChampionUnknown, BuildErrors.StepItemUnknown], verdict.Codes);
        Assert.Empty(verdict.UnavailableItems);
    }

    [Fact]
    public void TheModeCheckStacksOnTheValidatorCodes()
    {
        var structure = Items("3006") with { ChampionId = "Nobody" };

        var verdict = Evaluate(structure, GameMode.Aram);

        Assert.Equal([BuildErrors.ChampionUnknown, BuildErrors.ItemMode], verdict.Codes);
    }

    [Fact]
    public void AClassicItemIsRefusedInEveryModeAndQualifiedByItsId()
    {
        var verdict = BuildCatalogGate.Evaluate(
            Items("1055", "771055"),
            GameMode.SummonersRift,
            WithClassic);

        Assert.Equal([BuildErrors.ItemMode], verdict.Codes);
        Assert.Equal(["Doran's Blade [771055]"], verdict.UnavailableItems);
    }

    private static BuildStructureInput Items(params string[] items) =>
        BuildSamples.Valid with { Steps = [BuildSamples.Step("Start", null, items)] };

    private static StructureVerdict Evaluate(BuildStructureInput structure, GameMode mode) =>
        BuildCatalogGate.Evaluate(structure, mode, BuildSamples.Catalog);
}
