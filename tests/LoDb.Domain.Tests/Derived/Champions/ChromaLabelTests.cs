using LoDb.Domain.Derived.Champions;
using LoDb.Domain.Tests.Catalog.Samples;

namespace LoDb.Domain.Tests.Derived.Champions;

/// <summary>
/// A chroma is labelled by its accent hue unless the patch names the variant.
/// </summary>
public sealed class ChromaLabelTests
{
    private const string BaseSkin = "Spirit Blossom Ahri";

    [Fact]
    public void Up9AParentheticalVariantNameWins()
    {
        var chroma = ChampionSamples.Chroma(1, "Spirit Blossom Ahri (Ruby)", "#0ac8b9");

        Assert.Equal("Ruby", ChromaLabel.Of(chroma));
    }

    [Theory]
    [InlineData("not-a-hex")]
    [InlineData("#abc")]
    [InlineData("##c8aa6e")]
    public void Up9AnUnreadableColorIsAPlainChroma(string hex)
    {
        Assert.Equal(ChromaLabel.Unknown, LabelOf(hex));
    }

    [Fact]
    public void Up9AChromaWithoutColorIsAPlainChroma()
    {
        Assert.Equal(ChromaLabel.Unknown, ChromaLabel.Of(ChampionSamples.Chroma(1, BaseSkin)));
    }

    [Theory]
    [InlineData("#ffffff", "Pearl")]
    [InlineData("#000000", "Obsidian")]
    [InlineData("#808080", "Steel")]
    public void Up9GreysAreNamedByLightness(string hex, string expected)
    {
        Assert.Equal(expected, LabelOf(hex));
    }

    [Theory]
    [InlineData("#0a0020", "Obsidian")]
    [InlineData("#fffef0", "Pearl")]
    public void Up9NearBlackAndNearWhiteLoseTheirHue(string hex, string expected)
    {
        Assert.Equal(expected, LabelOf(hex));
    }

    [Theory]
    [InlineData("#ff0000", "Crimson")]
    [InlineData("#ffaa00", "Amber")]
    [InlineData("#00ff00", "Emerald")]
    [InlineData("#0ac8b9", "Teal")]
    [InlineData("#0000ff", "Azure")]
    [InlineData("#7a2bff", "Sapphire")]
    [InlineData("#ff00ff", "Violet")]
    [InlineData("#ff0080", "Rose")]
    public void Up9SaturatedAccentsAreBucketedByHue(string hex, string expected)
    {
        Assert.Equal(expected, LabelOf(hex));
    }

    [Fact]
    public void TheHashIsOptional()
    {
        Assert.Equal(LabelOf("#c8aa6e"), LabelOf("c8aa6e"));
        Assert.Equal(LabelOf("#0AC8B9"), LabelOf("#0ac8b9"));
    }

    private static string LabelOf(string hex) =>
        ChromaLabel.Of(ChampionSamples.Chroma(1, BaseSkin, hex));
}
