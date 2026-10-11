using LoDb.Domain.Versions;
using LoDb.Ingestion.Images;
using LoDb.Ingestion.Images.Art;
using LoDb.Ingestion.Tests.Ddragon;
using LoDb.Testing.Fixtures;

namespace LoDb.Ingestion.Tests.Images;

/// <summary>
/// The art variants of a champion: the centered splash at three widths and the loading
/// portrait at its own, fetched from the unversioned art of Data Dragon under Riot's
/// spelling, each a manifest row of its own.
/// </summary>
public sealed class ArtVariantsTests
{
    private static readonly PatchVersion Latest = DdragonFixtures.Latest;

    [Fact]
    public async Task ChampionHasItsCenteredWidthsThenItsLoadingPortrait()
    {
        using var harness = ReplayHarness.Create();
        var datasets = await VersionImagesTests.ReadAsync(harness, Latest);
        var fiddlesticks = datasets.Champions.Entries
            .Single(static entry => entry.Summary.Id == "Fiddlesticks");

        var variants = ArtVariants.Of(fiddlesticks);

        Assert.Equal(
            [
                "img/champion/centered/FiddleSticks_0.jpg@480",
                "img/champion/centered/FiddleSticks_0.jpg@960",
                "img/champion/centered/FiddleSticks_0.jpg@1280",
                "img/champion/loading/FiddleSticks_0.jpg@308",
            ],
            variants.Select(static variant => variant.ManifestKey));
        Assert.All(
            variants,
            static variant => Assert.Equal(ManifestTypes.ChampionArt, variant.ManifestType));
        Assert.Equal(
            new Uri(
                "https://ddragon.leagueoflegends.com/cdn/img/champion/centered/FiddleSticks_0.jpg"),
            variants[0].Url(Latest));
    }

    [Fact]
    public async Task VersionImagesLeaveTheArtToItsJob()
    {
        using var harness = ReplayHarness.Create();
        var datasets = await VersionImagesTests.ReadAsync(harness, Latest);

        var images = VersionImages.Of(datasets);

        Assert.All(images, static image => Assert.Null(image.Width));
    }

    [Fact]
    public void ArtHasAWidthAndIconsNone()
    {
        Assert.Throws<ArgumentException>(
            () => new DdragonImage(DdragonImageKind.CenteredArt, "img/champion/centered/A_0.jpg"));
        Assert.Throws<ArgumentException>(
            () => DdragonImage.ArtVariant(DdragonImageKind.Item, "1001.png", 480));
        Assert.Throws<ArgumentOutOfRangeException>(() => DdragonImage.ArtVariant(
            DdragonImageKind.LoadingArt,
            "img/champion/loading/A_0.jpg",
            0));
        var portrait = new DdragonImage(DdragonImageKind.Champion, "Ahri.png");
        Assert.Equal("Ahri.png", portrait.ManifestKey);
    }
}
