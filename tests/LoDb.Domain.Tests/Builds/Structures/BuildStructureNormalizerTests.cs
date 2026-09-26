using LoDb.Domain.Builds.Structures;
using LoDb.Domain.Tests.Builds.Samples;

namespace LoDb.Domain.Tests.Builds.Structures;

/// <summary>The canonical form a structure is stored in, as the legacy tests state it.</summary>
public sealed class BuildStructureNormalizerTests
{
    [Fact]
    public void EveryFieldIsCanonicalized()
    {
        var normalized = BuildStructureNormalizer.Normalize(new BuildStructureInput
        {
            ChampionId = "  Aatrox  ",
            Runes = BuildSamples.Runes,
            Steps =
            [
                BuildSamples.Step("  Start  ", "   ", "1055", "2003"),
                BuildSamples.Step("Core", " rush it ", "3006"),
            ],
        });

        Assert.Equal("Aatrox", normalized.ChampionId);
        Assert.Equal(BuildSamples.Precision, normalized.Runes.PrimaryStyleId);
        Assert.Equal([8005, 9101, 9104, 8014], normalized.Runes.PrimarySelections);
        Assert.Equal(BuildSamples.Domination, normalized.Runes.SecondaryStyleId);
        Assert.Equal([8126, 8138], normalized.Runes.SecondarySelections);
        Assert.Equal(["Start", "Core"], normalized.Steps.Select(static step => step.Label));
        Assert.Equal([null, "rush it"], normalized.Steps.Select(static step => step.Note));
        Assert.Equal(["1055", "2003"], normalized.Steps[0].Items);
    }

    [Fact]
    public void MalformedInputDegradesToEmptyShapes()
    {
        var normalized = BuildStructureNormalizer.Normalize(new BuildStructureInput());

        Assert.Equal(string.Empty, normalized.ChampionId);
        Assert.Equal(RunePage.Unset, normalized.Runes.PrimaryStyleId);
        Assert.Empty(normalized.Runes.PrimarySelections);
        Assert.Equal(RunePage.Unset, normalized.Runes.SecondaryStyleId);
        Assert.Empty(normalized.Runes.SecondarySelections);
        Assert.Empty(normalized.Steps);
    }

    [Fact]
    public void UnreadableEntriesBecomeUnsetOrEmpty()
    {
        var normalized = BuildStructureNormalizer.Normalize(new BuildStructureInput
        {
            Runes = new RunePageInput { PrimarySelections = [8005, null] },
            Steps = [null, BuildSamples.Step(null, null, "1055", null)],
        });

        Assert.Equal([8005, RunePage.Unset], normalized.Runes.PrimarySelections);
        Assert.Equal(string.Empty, normalized.Steps[0].Label);
        Assert.Empty(normalized.Steps[0].Items);
        Assert.Equal(["1055", string.Empty], normalized.Steps[1].Items);
    }
}
