using System.Globalization;
using System.Net;
using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Ddragon;
using LoDb.Ingestion.Egress.Errors;
using LoDb.Ingestion.Normalization;
using LoDb.Ingestion.Tests.Ddragon;
using LoDb.Testing.Fixtures;

namespace LoDb.Ingestion.Tests.Normalization;

/// <summary>
/// CommunityDragon's chromas merged into the Data Dragon skins (UP 9).
/// </summary>
public sealed class ChromaTests
{
    private const string AhriChromaAsset =
        "plugins/rcp-be-lol-game-data/global/default/v1/champion-chroma-images/103/103052.png";

    private static string LatestPatch => CommunityDragonUrls.Patch(DdragonFixtures.Latest);

    [Fact]
    public async Task SkinsCarryTheirChromasWithLowercasePatchPaths()
    {
        var ahri = await LatestAhriAsync();

        var dynasty = Assert.Single(ahri.Skins, static skin => skin.Id == "103001");

        var chroma = Assert.Single(dynasty.Chromas);
        Assert.Equal(103052, chroma.Id);
        Assert.Equal(["#2E38C4", "#C8003F"], chroma.Colors);
        Assert.Equal($"{LatestPatch}/{AhriChromaAsset}", chroma.Image);
        Assert.Equal(
            new Uri(CommunityDragonUrls.Root, $"{LatestPatch}/{AhriChromaAsset}"),
            CommunityDragonUrls.Asset(chroma.Image));
    }

    // Data Dragon lists "Popstar Ahri (Amethyst)" and the like as skins: they are chromas.
    [Fact]
    public async Task ChromasListedAsSkinsAreRemoved()
    {
        var ahri = await LatestAhriAsync();

        var ids = ahri.Skins.Select(static skin => skin.Id).ToList();
        var chromaIds = ahri.Skins
            .SelectMany(static skin => skin.Chromas)
            .Select(static chroma => chroma.Id.ToString(CultureInfo.InvariantCulture));

        Assert.Contains("103004", ids);
        Assert.DoesNotContain("103008", ids);
        Assert.Empty(ids.Intersect(chromaIds));
    }

    // CommunityDragon has no 3.13 directory: its newest patch stands in.
    [Fact]
    public async Task MissingPatchFallsBackToLatest()
    {
        using var harness = ReplayHarness.Create();

        var chromas = await harness.Datasets.ReadChromasAsync(
            PatchVersion.Parse("3.13.24"), ReplayHarness.Token);

        Assert.Equal(CommunityDragonUrls.LatestPatch, chromas.Patch);
        Assert.StartsWith("latest/", chromas.For("103001")[0].Image, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoPatchAtAllLeavesNoChroma()
    {
        using var harness = ReplayHarness.Create();
        harness.Replay.FailWith(IsCommunityDragon, HttpStatusCode.NotFound);

        var chromas = await harness.Datasets.ReadChromasAsync(
            DdragonFixtures.Latest, ReplayHarness.Token);

        Assert.Same(ChromaCatalog.Empty, chromas);
        Assert.Empty(chromas.For("103001"));
    }

    [Fact]
    public async Task OutageIsTransientNotEmpty()
    {
        using var harness = ReplayHarness.Create();
        harness.Replay.FailWith(IsCommunityDragon, HttpStatusCode.BadGateway);

        await Assert.ThrowsAsync<EgressTransientException>(
            () => harness.Datasets.ReadChromasAsync(DdragonFixtures.Latest, ReplayHarness.Token));
    }

    [Fact]
    public void AssetPathsAreLowercase()
    {
        var path = CommunityDragonUrls.AssetPath(
            "16.19", "/lol-game-data/assets/v1/Champion-Chroma-Images/103/103052.PNG");

        Assert.Equal($"16.19/{AhriChromaAsset}", path);
    }

    private static bool IsCommunityDragon(Uri url) => url.Host == CommunityDragonUrls.Root.Host;

    private static async Task<ChampionDetail> LatestAhriAsync()
    {
        using var harness = ReplayHarness.Create();
        var chromas = await harness.Datasets.ReadChromasAsync(
            DdragonFixtures.Latest, ReplayHarness.Token);
        Assert.Equal(LatestPatch, chromas.Patch);
        var scope = ReplayHarness.Scope(DdragonFixtures.Latest.Value, "en_US");
        var champions = await harness.Datasets.ReadChampionsAsync(
            scope, chromas, ReplayHarness.Token);
        return Assert.Single(champions.Entries, static champion => champion.Summary.Id == "Ahri");
    }
}
