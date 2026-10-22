using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.States.Projections;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Normalization;
using LoDb.Ingestion.Tests.Ddragon;

namespace LoDb.Ingestion.Tests.Catalog.States.Pairs;

/// <summary>
/// The known limit of the skins fields: Data Dragon lists each chroma as a skin, and only
/// CommunityDragon's catalog takes it out (<c>ChromaSkins</c>). A version ingested before
/// CommunityDragon knows its new chroma keeps that chroma as a skin, so a chroma alone opens
/// a state. These tests pin the dependency; the fix (Data Dragon's own <c>parentSkin</c>) sits
/// in the ingestion mapper, and turns them into "no change".
/// </summary>
public sealed class ChromaSourcePairTests
{
    private const string NewChromaId = "103096";

    [Fact]
    public async Task AStaleChromaCatalogKeepsTheNewChromaAsASkin()
    {
        var before = await AhriAsync("16.18.1", ChromasOf("16.18.1"));
        var after = await AhriAsync("16.19.1", ChromasOf("16.18.1"));

        Assert.Equal(
            ["skins", $"skin.{NewChromaId}.name"],
            CatalogPairs.ChangedKeys(
                ChampionProjection.Of(before),
                ChampionProjection.Of(after)));
    }

    [Fact]
    public async Task WithoutChromaCatalogEveryChromaIsASkin()
    {
        var known = await AhriAsync("16.19.1", ChromasOf("16.19.1"));
        var unknown = await AhriAsync("16.19.1", static _ => Task.FromResult(ChromaCatalog.Empty));

        Assert.DoesNotContain(known.Skins, static skin => skin.Id == NewChromaId);
        Assert.Contains(unknown.Skins, static skin => skin.Id == NewChromaId);
        Assert.Contains(
            "skins",
            CatalogPairs.ChangedKeys(
                ChampionProjection.Of(known),
                ChampionProjection.Of(unknown)));
    }

    private static Func<ReplayHarness, Task<ChromaCatalog>> ChromasOf(string version) =>
        harness => harness.Datasets.ReadChromasAsync(
            PatchVersion.Parse(version),
            ReplayHarness.Token);

    // Ahri's en_US entry as the ingestion maps it with the chroma catalog given.
    private static async Task<ChampionDetail> AhriAsync(
        string version,
        Func<ReplayHarness, Task<ChromaCatalog>> chromas)
    {
        using var harness = ReplayHarness.Create();
        var champions = await harness.Datasets.ReadChampionsAsync(
            ReplayHarness.Scope(version, CatalogFixtures.English.Code),
            await chromas(harness),
            ReplayHarness.Token);
        return Assert.Single(
            champions.Entries,
            static champion => champion.Summary.Id == "Ahri");
    }
}
