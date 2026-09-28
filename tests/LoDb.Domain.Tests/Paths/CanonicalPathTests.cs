using LoDb.Domain.Catalog;
using LoDb.Domain.Paths;

namespace LoDb.Domain.Tests.Paths;

/// <summary>
/// The four entity path shapes: champions and summoner spells by id, items and rune paths by
/// id and slug.
/// </summary>
public sealed class CanonicalPathTests
{
    [Fact]
    public void AChampionIsAddressedByItsId()
    {
        var path = CanonicalPath.Champion("MonkeyKing");

        Assert.Equal("champions/MonkeyKing", path.Value);
        Assert.Equal(ResourceType.Champions, path.Type);
        Assert.Equal("MonkeyKing", path.Id);
        Assert.Empty(path.Slug);
    }

    [Fact]
    public void ASummonerSpellIsAddressedByItsId()
    {
        var path = CanonicalPath.Summoner("SummonerFlash_Jade");

        Assert.Equal("summoners/SummonerFlash_Jade", path.Value);
    }

    [Fact]
    public void AnItemCarriesItsIdAndSlug()
    {
        var path = CanonicalPath.Item("3078", "Trinity Force");

        Assert.Equal("items/3078-trinity-force", path.Value);
        Assert.Equal("items/3078-trinity-force", path.ToString());
        Assert.Equal("trinity-force", path.Slug);
    }

    [Fact]
    public void ARunePathCarriesItsIdAndSlug()
    {
        Assert.Equal("runes/8000-precision", CanonicalPath.RuneTree(8000, "Precision").Value);
    }

    [Fact]
    public void ANameWithoutSlugLeavesTheIdAlone()
    {
        Assert.Equal("items/2008", CanonicalPath.Item("2008", string.Empty).Value);
        Assert.Equal("items/2008", CanonicalPath.Item("2008", null).Value);
    }

    [Theory]
    [InlineData(ResourceType.Champions, "champions")]
    [InlineData(ResourceType.Items, "items")]
    [InlineData(ResourceType.Runes, "runes")]
    [InlineData(ResourceType.Summoners, "summoners")]
    public void EachResourceHasItsSegment(ResourceType type, string segment)
    {
        Assert.Equal(segment, CanonicalPath.SegmentOf(type));
    }

    [Theory]
    [InlineData("Monkey King")]
    [InlineData("../admin")]
    [InlineData("Ahri/")]
    [InlineData("")]
    public void AChampionIdThatCouldBreakThePathIsRefused(string id)
    {
        Assert.Throws<ArgumentException>(() => CanonicalPath.Champion(id));
    }

    [Theory]
    [InlineData("3078a")]
    [InlineData("-1")]
    [InlineData("")]
    public void AnItemIdMustBeNumeric(string id)
    {
        Assert.Throws<ArgumentException>(() => CanonicalPath.Item(id, "Trinity Force"));
    }

    [Fact]
    public void ANegativeRunePathIdIsRefused()
    {
        Assert.Throws<ArgumentException>(() => CanonicalPath.RuneTree(-1, "Precision"));
    }
}
