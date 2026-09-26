using LoDb.Domain.Derived.Champions;
using LoDb.Domain.Tests.Catalog.Samples;

namespace LoDb.Domain.Tests.Derived.Champions;

/// <summary>
/// The base skin, named "default" in every language, displays as the champion.
/// </summary>
public sealed class SkinNameTests
{
    [Fact]
    public void TheDefaultSkinDisplaysAsTheChampion()
    {
        var skin = ChampionSamples.Skin("103000", "default");

        Assert.Equal("Ahri", SkinName.Display(skin, "Ahri"));
    }

    [Theory]
    [InlineData("Dynasty Ahri")]
    [InlineData("Default")]
    public void AnyOtherNameIsKept(string name)
    {
        var skin = ChampionSamples.Skin("103001", name);

        Assert.Equal(name, SkinName.Display(skin, "Ahri"));
    }
}
