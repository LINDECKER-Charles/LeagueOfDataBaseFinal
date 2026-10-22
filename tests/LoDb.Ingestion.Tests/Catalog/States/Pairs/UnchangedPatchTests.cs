using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.States.Fingerprints;
using LoDb.Domain.States.Projections;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Ingestion.Tests.Catalog.States.Pairs;

/// <summary>
/// 16.18.1 to 16.19.1 changes nothing a page shows in the recording: every entity keeps its
/// state, Ahri included, whose skins differ by a CommunityDragon chroma alone.
/// </summary>
public sealed class UnchangedPatchTests
{
    [Fact]
    public async Task EveryEntityKeepsItsFingerprint()
    {
        var before = Fingerprints(await CatalogPairs.EnglishAsync("16.18.1"));
        var after = Fingerprints(await CatalogPairs.EnglishAsync("16.19.1"));

        Assert.NotEmpty(before);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task AChromaAloneKeepsAhrisState()
    {
        var before = await CatalogPairs.EnglishAsync("16.18.1");
        var after = await CatalogPairs.EnglishAsync("16.19.1");
        var ahriBefore = Ahri(before);
        var ahriAfter = Ahri(after);

        Assert.NotEqual(Chromas(ahriBefore), Chromas(ahriAfter));
        Assert.Equal(
            EntityFingerprint.Of(ChampionProjection.Of(ahriBefore)),
            EntityFingerprint.Of(ChampionProjection.Of(ahriAfter)));
    }

    private static ChampionDetail Ahri(CatalogSnapshot catalog) =>
        CatalogPairs.Listed(
            ListedEntities.Champions(catalog),
            "Ahri",
            static champion => champion.Summary.Id);

    private static List<int> Chromas(ChampionDetail champion) =>
        [.. champion.Skins
            .SelectMany(static skin => skin.Chromas)
            .Select(static chroma => chroma.Id)];

    private static SortedDictionary<string, string> Fingerprints(CatalogSnapshot catalog)
    {
        var fingerprints = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var champion in ListedEntities.Champions(catalog))
        {
            fingerprints.Add(
                "champions/" + champion.Summary.Id,
                Of(ChampionProjection.Of(champion)));
        }

        foreach (var item in ListedEntities.Items(catalog))
        {
            fingerprints.Add("items/" + item.Id, Of(ItemProjection.Of(item)));
        }

        foreach (var tree in ListedEntities.RuneTrees(catalog))
        {
            fingerprints.Add("runes/" + tree.Id, Of(RuneTreeProjection.Of(tree)));
        }

        foreach (var spell in ListedEntities.Summoners(catalog))
        {
            fingerprints.Add("summoners/" + spell.Id, Of(SummonerProjection.Of(spell)));
        }

        return fingerprints;
    }

    private static string Of(IReadOnlyList<ProjectedField> projection) =>
        EntityFingerprint.Of(projection);
}
