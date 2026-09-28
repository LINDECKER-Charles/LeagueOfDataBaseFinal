using LoDb.Domain.Catalog.Champions;
using LoDb.Ingestion.Normalization;
using LoDb.Ingestion.Tests.Ddragon;
using LoDb.Testing.Fixtures;

namespace LoDb.Ingestion.Tests.Normalization;

/// <summary>
/// The champion traps of old versions (UP 3), read from the recording.
/// </summary>
public sealed class ChampionDatasetTests
{
    [Fact]
    public async Task ZeroVersionsHaveNoPartype()
    {
        var champions = await ReadAsync("0.151.2");

        Assert.All(champions, static champion => Assert.Null(champion.Summary.Partype));
        Assert.All(await ReadAsync("3.13.24"), static champion =>
            Assert.False(string.IsNullOrEmpty(champion.Summary.Partype)));
    }

    // 3.13.8 omits num: the art index is the position in the list.
    [Fact]
    public async Task SkinsWithoutNumAreNumberedByPosition()
    {
        var ahri = Single(await ReadAsync("3.13.8"), "Ahri");

        Assert.NotEmpty(ahri.Skins);
        Assert.Equal(
            Enumerable.Range(0, ahri.Skins.Count),
            ahri.Skins.Select(static skin => skin.Number));
        Assert.Equal("103000", ahri.Skins[0].Id);
    }

    // 0.151.2 writes neither skin ids nor num, and spells FiddleSticks the internal way.
    [Fact]
    public async Task ZeroVersionSkinsHaveNoIdAndNoChroma()
    {
        var fiddlesticks = Single(await ReadAsync("0.151.2"), "FiddleSticks");

        Assert.NotEmpty(fiddlesticks.Skins);
        Assert.All(fiddlesticks.Skins, static skin => Assert.Equal(string.Empty, skin.Id));
        Assert.All(fiddlesticks.Skins, static skin => Assert.Empty(skin.Chromas));
        Assert.Equal(
            Enumerable.Range(0, fiddlesticks.Skins.Count),
            fiddlesticks.Skins.Select(static skin => skin.Number));
    }

    // 3.6.14 has no championFull.json and answers 403 for every detail file but Ahri's.
    [Fact]
    public async Task ForbiddenDetailFallsBackToTheSummary()
    {
        var champions = await ReadAsync("3.6.14");

        Assert.Equal(["Ahri", "FiddleSticks", "Garen", "MonkeyKing", "Teemo"], Ids(champions));
        Assert.NotEmpty(Single(champions, "Ahri").Spells);
        Assert.All(
            champions.Where(static champion => champion.Summary.Id != "Ahri"),
            AssertSummaryOnly);
    }

    [Fact]
    public async Task ChampionFullIsOneRequestPerLanguage()
    {
        using var harness = ReplayHarness.Create();
        var scope = ReplayHarness.Scope(DdragonFixtures.Latest.Value, "fr_FR");

        var champions = await harness.Datasets.ReadChampionsAsync(
            scope, ChromaCatalog.Empty, ReplayHarness.Token);

        Assert.Equal(
            ["Ahri", "Fiddlesticks", "Garen", "MonkeyKing", "Teemo"],
            Ids(champions.Entries));
        var request = Assert.Single(harness.Replay.Requests);
        Assert.EndsWith("/fr_FR/championFull.json", request.AbsolutePath, StringComparison.Ordinal);
    }

    private static void AssertSummaryOnly(ChampionDetail champion)
    {
        Assert.False(string.IsNullOrEmpty(champion.Summary.Name));
        Assert.Empty(champion.Spells);
        Assert.Empty(champion.Skins);
        Assert.Null(champion.Passive);
    }

    private static IEnumerable<string> Ids(IEnumerable<ChampionDetail> champions) =>
        champions.Select(static champion => champion.Summary.Id);

    private static ChampionDetail Single(IEnumerable<ChampionDetail> champions, string id) =>
        Assert.Single(champions, champion => champion.Summary.Id == id);

    private static async Task<IReadOnlyList<ChampionDetail>> ReadAsync(string version)
    {
        using var harness = ReplayHarness.Create();
        var scope = ReplayHarness.Scope(version, "en_US");
        var chromas = await harness.Datasets.ReadChromasAsync(scope.Version, ReplayHarness.Token);
        var champions = await harness.Datasets.ReadChampionsAsync(
            scope, chromas, ReplayHarness.Token);
        Assert.Equal("en_US", champions.ContentLanguage);
        return champions.Entries;
    }
}
