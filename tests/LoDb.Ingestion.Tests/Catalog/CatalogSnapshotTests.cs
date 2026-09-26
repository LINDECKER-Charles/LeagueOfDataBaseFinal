using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Snapshots;
using LoDb.Ingestion.Tests.Ddragon;
using LoDb.Testing.Fixtures;

namespace LoDb.Ingestion.Tests.Catalog;

/// <summary>
/// A catalog as the pages read it: entries by id, twins, runes by id, and the paths and
/// resource tokens every language shares with en_US.
/// </summary>
public sealed class CatalogSnapshotTests
{
    private static readonly DdragonLanguage French = CatalogFixtures.French;

    [Fact]
    public async Task EntriesAreFoundByTheirId()
    {
        var catalog = await CatalogFixtures.LatestAsync(CatalogFixtures.English);

        Assert.Equal("Wukong", catalog.Champions.Find("MonkeyKing")?.Summary.Name);
        Assert.Equal("Trinity Force", catalog.Items.Find("3078")?.Name);
        Assert.Equal("Precision", catalog.Runes.Find("8000")?.Name);
        Assert.Equal("Flash", catalog.Summoners.Find("SummonerFlash_Jade")?.Name);
        Assert.Null(catalog.Champions.Find("monkeyking"));
        Assert.Null(catalog.Items.Find("9999"));
        Assert.Equal(
            ["Ahri", "Fiddlesticks", "Garen", "MonkeyKing", "Teemo"],
            catalog.Champions.Entries.Select(static champion => champion.Summary.Id));
        Assert.Equal(CatalogFixtures.English, catalog.Items.ContentLanguage);
    }

    [Fact]
    public async Task ListedItemsLeaveTheDebrisOut()
    {
        var catalog = await CatalogFixtures.LatestAsync(CatalogFixtures.English);

        var listed = catalog.ListedItems.Select(static item => item.Id).ToList();

        Assert.Equal(18, catalog.Items.Entries.Count);
        Assert.Equal(13, listed.Count);
        Assert.All(
            (string[])["2008", "7050", "226660", "772139", "772140"],
            debris => Assert.DoesNotContain(debris, listed));
        Assert.Equal("Fire at Will", catalog.Items.Find("3901")?.Name);
        Assert.NotNull(catalog.Items.Find("7050"));
    }

    [Fact]
    public async Task TwinsAreFoundBothWays()
    {
        var catalog = await CatalogFixtures.LatestAsync(French);

        Assert.Equal("771004", catalog.TwinOf(catalog.Items.Find("1004")!)?.Id);
        Assert.Equal("1004", catalog.TwinOf(catalog.Items.Find("771004")!)?.Id);
        Assert.Null(catalog.TwinOf(catalog.Items.Find("773001")!));
        Assert.Equal(
            "SummonerFlash_Jade",
            catalog.TwinOf(catalog.Summoners.Find("SummonerFlash")!)?.Id);
        Assert.Equal(
            "SummonerHeal",
            catalog.TwinOf(catalog.Summoners.Find("SummonerHeal_Jade")!)?.Id);
        Assert.Null(catalog.TwinOf(catalog.Summoners.Find("SummonerExhaust")!));
    }

    [Fact]
    public async Task RunesAreFoundWithTheirPathAndSlot()
    {
        var catalog = await CatalogFixtures.LatestAsync(French);

        var keystone = catalog.FindRune(8005);
        var last = catalog.FindRune(8299);

        Assert.NotNull(keystone);
        Assert.NotNull(last);
        Assert.Equal((8000, 0, 8005), (keystone.Tree.Id, keystone.SlotIndex, keystone.Rune.Id));
        Assert.Equal((8000, 3, 8299), (last.Tree.Id, last.SlotIndex, last.Rune.Id));
        Assert.Equal(8100, catalog.FindRune(8112)?.Tree.Id);
        Assert.Null(catalog.FindRune(8000));
    }

    [Fact]
    public async Task PathsAndResourceTokensAreReadInEnglish()
    {
        var catalog = await CatalogFixtures.LatestAsync(French);
        var trinity = catalog.Items.Find("3078")!;
        var precision = catalog.Runes.Find("8000")!;
        var garen = catalog.Champions.Find("Garen")!;

        Assert.Equal("Force de la trinité", trinity.Name);
        Assert.Equal("items/3078-trinity-force", catalog.PathOf(trinity).Value);
        Assert.Equal("Précision", precision.Name);
        Assert.Equal("runes/8000-precision", catalog.PathOf(precision).Value);
        Assert.Equal("Aucune", garen.Summary.Partype);
        Assert.Equal("none", catalog.ResourceTokenOf(garen));
        Assert.Equal("mana", catalog.ResourceTokenOf(catalog.Champions.Find("Ahri")!));
        Assert.Equal(
            "champions/MonkeyKing",
            catalog.PathOf(catalog.Champions.Find("MonkeyKing")!).Value);
        Assert.Equal(
            "summoners/SummonerFlash_Jade",
            catalog.PathOf(catalog.Summoners.Find("SummonerFlash_Jade")!).Value);
    }

    [Fact]
    public async Task VersionWithoutRunesHasAnEmptyRuneResource()
    {
        var catalog = await CatalogFixtures.ReadAsync(PatchVersion.Parse("7.21.1"), French);

        Assert.Empty(catalog.Runes.Entries);
        Assert.Null(catalog.Runes.ContentLanguage);
        Assert.Null(catalog.FindRune(8005));
        Assert.Equal(French, catalog.Champions.ContentLanguage);
    }

    [Fact]
    public async Task EnglishCatalogMustBeOfTheSameVersion()
    {
        using var harness = ReplayHarness.Create();
        var english = await CatalogFixtures.LatestAsync(CatalogFixtures.English);
        var datasets = await CatalogFixtures.DatasetsAsync(
            harness,
            CatalogFixtures.Latest,
            French);

        Assert.Throws<ArgumentException>(() => new CatalogSnapshot(
            DdragonFixtures.Previous,
            French,
            datasets,
            english));
    }
}
