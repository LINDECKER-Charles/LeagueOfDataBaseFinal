using System.Globalization;
using LoDb.Domain.Paths;

namespace LoDb.Domain.Tests.Paths;

/// <summary>
/// Slugs are readable ASCII taken from the en_US name, the same in every locale; the id keeps
/// namesakes apart.
/// </summary>
public sealed class SlugTests
{
    [Theory]
    [InlineData("Rabadon's Deathcap", "rabadons-deathcap")]
    [InlineData("Zaz'Zak's Realmspike", "zazzaks-realmspike")]
    [InlineData("Jak’Sho, The Protean", "jaksho-the-protean")]
    [InlineData("Guardianʼs Horn", "guardians-horn")]
    public void ApostrophesVanishInsideWords(string name, string expected)
    {
        Assert.Equal(expected, CanonicalPath.Item("1001", name).Slug);
    }

    [Theory]
    [InlineData("Épée de Démacia", "epee-de-demacia")]
    [InlineData("Feu à volonté", "feu-a-volonte")]
    [InlineData("Coup de Grâce", "coup-de-grace")]
    public void AccentsFoldToTheirBaseLetter(string name, string expected)
    {
        Assert.Equal(expected, CanonicalPath.Item("1001", name).Slug);
    }

    [Theory]
    [InlineData("B. F. Sword", "b-f-sword")]
    [InlineData("  Legend: Alacrity  ", "legend-alacrity")]
    [InlineData("Poro-Snax", "poro-snax")]
    [InlineData("Total Biscuit of Everlasting Will", "total-biscuit-of-everlasting-will")]
    public void OtherRunsBecomeOneHyphen(string name, string expected)
    {
        Assert.Equal(expected, CanonicalPath.Item("1001", name).Slug);
    }

    [Fact]
    public void MarkupNeverReachesASlug()
    {
        var path = CanonicalPath.Item(
            "3901",
            "<rarityLegendary>Fire at Will</rarityLegendary><br><subtitleLeft>500</subtitleLeft>");

        Assert.Equal("items/3901-fire-at-will", path.Value);
    }

    [Fact]
    public void ANonLatinNameLeavesTheIdAlone()
    {
        Assert.Equal("items/1001", CanonicalPath.Item("1001", "鞋子").Value);
    }

    [Fact]
    public void Up6HomonymousClassicTwinsGetDistinctPaths()
    {
        var modern = CanonicalPath.Item("1036", "Long Sword");
        var classic = CanonicalPath.Item("771036", "Long Sword");

        Assert.Equal("items/1036-long-sword", modern.Value);
        Assert.Equal("items/771036-long-sword", classic.Value);
        Assert.NotEqual(modern, classic);
    }

    [Fact]
    public void TheSlugIgnoresTheCurrentCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            var path = CanonicalPath.Item("1001", "INFINITY EDGE");

            Assert.Equal("items/1001-infinity-edge", path.Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
