using LoDb.Domain.Builds.Rules;
using LoDb.Domain.Builds.Structures;
using LoDb.Domain.Tests.Builds.Samples;

namespace LoDb.Domain.Tests.Builds.Rules;

/// <summary>
/// The whole structure, as the legacy validator tests check it: every section is reported,
/// each code once, in the order champion, runes, steps.
/// </summary>
public sealed class BuildStructureValidatorTests
{
    [Fact]
    public void AValidStructurePasses() =>
        Assert.Empty(BuildStructureValidator.Validate(BuildSamples.Valid, BuildSamples.Catalog));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AMissingChampionIsAStructureError(string? championId)
    {
        var structure = BuildSamples.Valid with { ChampionId = championId };

        Assert.Equal([BuildErrors.StructureInvalid], Validate(structure));
    }

    [Fact]
    public void AnUnknownChampionIsNamedSo()
    {
        var structure = BuildSamples.Valid with { ChampionId = "Teemo" };

        Assert.Equal([BuildErrors.ChampionUnknown], Validate(structure));
    }

    [Fact]
    public void AChampionIdIsTrimmedBeforeItIsLookedUp() =>
        Assert.Empty(Validate(BuildSamples.Valid with { ChampionId = "  Ahri " }));

    [Fact]
    public void RunesThatAreNoObjectAreAStructureError()
    {
        var structure = BuildSamples.Valid with { Runes = null };

        Assert.Equal([BuildErrors.StructureInvalid], Validate(structure));
    }

    [Fact]
    public void ErrorsAccumulateAcrossSectionsAndAppearOnce()
    {
        // Two wrong-slot picks and two unknown items: each code once.
        var structure = BuildSamples.WithRunes(static runes => runes with
        {
            PrimarySelections = [8005, 9104, 9101, 8014],
        }) with
        {
            ChampionId = "Teemo",
        };
        structure = structure with
        {
            Steps = [BuildSamples.Step("Start", null, "9999", "8888"), .. structure.Steps!.Skip(1)],
        };

        Assert.Equal(
            [BuildErrors.ChampionUnknown, BuildErrors.PrimarySlot, BuildErrors.StepItemUnknown],
            Validate(structure));
    }

    [Fact]
    public void AnEmptyStructureReportsEverySection() =>
        Assert.Equal(
            [BuildErrors.StructureInvalid, BuildErrors.StepsCount],
            Validate(new BuildStructureInput()));

    private static IReadOnlyList<string> Validate(BuildStructureInput structure) =>
        BuildStructureValidator.Validate(structure, BuildSamples.Catalog);
}
