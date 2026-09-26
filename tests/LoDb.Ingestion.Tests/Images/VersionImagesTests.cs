using LoDb.Domain.Versions;
using LoDb.Ingestion.Images;
using LoDb.Ingestion.Pipeline.Datasets;
using LoDb.Ingestion.Tests.Ddragon;
using LoDb.Testing.Fixtures;

namespace LoDb.Ingestion.Tests.Images;

/// <summary>
/// The images a version names, each manifest key once, and the URL each is fetched from.
/// </summary>
public sealed class VersionImagesTests
{
    private static readonly PatchVersion Latest = DdragonFixtures.Latest;

    private static CancellationToken Token => ReplayHarness.Token;

    [Fact]
    public async Task LatestVersionNamesEveryImageOnce()
    {
        using var harness = ReplayHarness.Create();
        var datasets = await ReadAsync(harness, Latest);

        var images = VersionImages.Of(datasets);

        Assert.Equal(81, images.Count);
        var byKind = images.CountBy(static image => image.Kind).ToDictionary();
        Assert.Equal(5, byKind[DdragonImageKind.Champion]);
        Assert.Equal(5, byKind[DdragonImageKind.Passive]);
        Assert.Equal(20, byKind[DdragonImageKind.ChampionSpell]);
        Assert.Equal(18, byKind[DdragonImageKind.Item]);
        Assert.Equal(6, byKind[DdragonImageKind.SummonerSpell]);
        Assert.Equal(27, byKind[DdragonImageKind.Rune]);
        Assert.Equal(
            images.Count,
            images.DistinctBy(static image => (image.ManifestType, image.File)).Count());
    }

    [Fact]
    public async Task ChampionNamesItsPortraitPassiveAndAbilities()
    {
        using var harness = ReplayHarness.Create();
        var datasets = await ReadAsync(harness, Latest);
        var ahri = datasets.Champions.Entries.Single(static entry => entry.Summary.Id == "Ahri");

        var images = VersionImages.Of(ahri);

        Assert.Equal(
            [
                DdragonImageKind.Champion,
                DdragonImageKind.Passive,
                DdragonImageKind.ChampionSpell,
                DdragonImageKind.ChampionSpell,
                DdragonImageKind.ChampionSpell,
                DdragonImageKind.ChampionSpell,
            ],
            images.Select(static image => image.Kind));
        Assert.All(
            images,
            static image => Assert.Equal(ManifestTypes.Champion, image.ManifestType));
        Assert.Equal(
            new Uri("https://ddragon.leagueoflegends.com/cdn/16.19.1/img/champion/Ahri.png"),
            images[0].Url(Latest));
    }

    [Theory]
    [InlineData(DdragonImageKind.Champion, "cdn/16.19.1/img/champion/", ManifestTypes.Champion)]
    [InlineData(DdragonImageKind.Passive, "cdn/16.19.1/img/passive/", ManifestTypes.Champion)]
    [InlineData(DdragonImageKind.ChampionSpell, "cdn/16.19.1/img/spell/", ManifestTypes.Champion)]
    [InlineData(DdragonImageKind.Item, "cdn/16.19.1/img/item/", ManifestTypes.Item)]
    [InlineData(DdragonImageKind.SummonerSpell, "cdn/16.19.1/img/spell/", ManifestTypes.Summoner)]
    [InlineData(DdragonImageKind.Rune, "cdn/img/", ManifestTypes.Runes)]
    public void ImageIsFetchedFromTheFolderOfItsKind(
        DdragonImageKind kind,
        string folder,
        string manifestType)
    {
        var image = new DdragonImage(kind, "Icon.png");

        var url = image.Url(Latest);

        Assert.Equal(new Uri($"https://ddragon.leagueoflegends.com/{folder}Icon.png"), url);
        Assert.Equal(manifestType, image.ManifestType);
    }

    [Fact]
    public void ImageNameMustFitTheManifest()
    {
        Assert.Throws<ArgumentException>(() => new DdragonImage(DdragonImageKind.Item, " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DdragonImage(
            DdragonImageKind.Item,
            new string('a', DdragonImage.MaxFileLength + 1)));
    }

    internal static async Task<VersionDatasets> ReadAsync(
        ReplayHarness harness,
        PatchVersion version)
    {
        var scope = ReplayHarness.Scope(version.Value, "en_US");
        var chromas = await harness.Datasets.ReadChromasAsync(version, Token);
        return new VersionDatasets
        {
            Champions = await harness.Datasets.ReadChampionsAsync(scope, chromas, Token),
            Items = await harness.Datasets.ReadItemsAsync(scope, Token),
            Runes = await harness.Datasets.ReadRunesAsync(scope, Token),
            Summoners = await harness.Datasets.ReadSummonersAsync(scope, Token),
        };
    }
}
