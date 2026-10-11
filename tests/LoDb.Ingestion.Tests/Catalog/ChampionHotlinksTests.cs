using LoDb.Domain.Paths;
using LoDb.Ingestion.Catalog.Hotlinks;

namespace LoDb.Ingestion.Tests.Catalog;

/// <summary>
/// The champion media linked where they live: art from Data Dragon under Riot's spelling,
/// swatches from CommunityDragon, ability videos by the champion's key on four digits.
/// </summary>
public sealed class ChampionHotlinksTests
{
    private const string ArtRoot = "https://ddragon.leagueoflegends.com/cdn/img/champion/";
    private const string VideoRoot = "https://d28xe8vt774jo5.cloudfront.net/champion-abilities/";

    // The spelling and the kinds are ChampionArt's (domain tests): one row proves the link
    // goes through it.
    [Fact]
    public void ArtIsLinkedUnderRiotsSpelling()
    {
        var url = ChampionHotlinks.Art("Fiddlesticks", ChampionArtKind.Centered, 27);

        Assert.Equal(new Uri($"{ArtRoot}centered/FiddleSticks_27.jpg"), url);
    }

    [Fact]
    public async Task VideosAreKeyedByTheKeyOnFourDigits()
    {
        var catalog = await CatalogFixtures.LatestAsync(CatalogFixtures.English);
        var fiddlesticks = catalog.Champions.Find("Fiddlesticks")!.Summary;
        var ahri = catalog.Champions.Find("Ahri")!.Summary;

        var passive = ChampionHotlinks.Video(fiddlesticks, AbilitySlot.Passive);
        var ultimate = ChampionHotlinks.Video(ahri, AbilitySlot.R);

        Assert.NotNull(passive);
        Assert.NotNull(ultimate);
        Assert.Equal(new Uri($"{VideoRoot}0009/ability_0009_P1.webm"), passive.Webm);
        Assert.Equal(new Uri($"{VideoRoot}0009/ability_0009_P1.mp4"), passive.Mp4);
        Assert.Equal(new Uri($"{VideoRoot}0009/ability_0009_P1.jpg"), passive.Poster);
        Assert.Equal(new Uri($"{VideoRoot}0103/ability_0103_R1.webm"), ultimate.Webm);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ahri")]
    [InlineData("-103")]
    public async Task KeyThatIsNotANumberNamesNoVideo(string key)
    {
        var catalog = await CatalogFixtures.LatestAsync(CatalogFixtures.English);
        var ahri = catalog.Champions.Find("Ahri")!.Summary with { Key = key };

        Assert.Null(ChampionHotlinks.Video(ahri, AbilitySlot.Q));
    }

    [Fact]
    public async Task ChromaSwatchIsLinkedOnCommunityDragon()
    {
        var catalog = await CatalogFixtures.LatestAsync(CatalogFixtures.French);
        var chroma = catalog.Champions.Find("Ahri")!.Skins
            .SelectMany(static skin => skin.Chromas)
            .First();

        var url = ChampionHotlinks.ChromaSwatch(chroma);

        // Under the patch that answered the recording, as the ingestion stored it (UP 9).
        Assert.Equal(
            new Uri(
                "https://raw.communitydragon.org/16.19/plugins/rcp-be-lol-game-data/global/"
                + $"default/v1/champion-chroma-images/103/{chroma.Id}.png"),
            url);
    }
}
