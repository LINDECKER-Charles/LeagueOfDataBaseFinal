using LoDb.Domain.Paths;

namespace LoDb.Domain.Tests.Paths;

/// <summary>
/// Champion art is hotlinked, unversioned, under Riot's internal spelling of the champion.
/// </summary>
public sealed class ChampionArtTests
{
    [Theory]
    [InlineData(ChampionArtKind.Splash, "img/champion/splash/FiddleSticks_27.jpg")]
    [InlineData(ChampionArtKind.Loading, "img/champion/loading/FiddleSticks_27.jpg")]
    [InlineData(ChampionArtKind.Centered, "img/champion/centered/FiddleSticks_27.jpg")]
    public void Up7FiddlesticksArtUsesTheInternalSpelling(ChampionArtKind kind, string expected)
    {
        Assert.Equal(expected, DdragonImagePath.ChampionArt("Fiddlesticks", kind, 27));
    }

    [Fact]
    public void OtherChampionsKeepTheirPublicId()
    {
        Assert.Equal(
            "img/champion/splash/MonkeyKing_3.jpg",
            DdragonImagePath.ChampionArt("MonkeyKing", ChampionArtKind.Splash, 3));
        Assert.Equal(
            "img/champion/splash/Ahri_0.jpg",
            DdragonImagePath.ChampionArt("Ahri", ChampionArtKind.Splash, 0));
    }

    [Fact]
    public void Up8ArtPathsCarryNoVersion()
    {
        var path = DdragonImagePath.ChampionArt("Ahri", ChampionArtKind.Loading, 1);

        Assert.StartsWith("img/", path, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"\d+\.\d+", path);
    }
}
