using LoDb.Domain.Catalog.Items;
using LoDb.Ingestion.Pipeline.Datasets;
using LoDb.Ingestion.Tests.Ddragon;

namespace LoDb.Ingestion.Tests.Catalog.States.Building;

/// <summary>
/// The en_US datasets of the recording's latest version, reduced to a few entities of each
/// resource, and the changes a test makes to them from one test-owned version to the next.
/// </summary>
internal static class ReducedDatasets
{
    public const string Boots = "1001";
    public const string Potion = "2003";
    public const string Evenshroud = "3001";
    public const string Trinity = "3078";

    private static readonly string[] Champions = ["Ahri", "Fiddlesticks"];
    private static readonly string[] Items = [Boots, Potion, Evenshroud, Trinity];
    private static readonly int[] RuneTrees = [8000, 8100];
    private static readonly string[] Summoners = ["SummonerFlash", "SummonerHeal"];

    /// <summary>Two champions, four items, two rune paths and two summoner spells.</summary>
    public static async Task<VersionDatasets> ReadAsync()
    {
        using var replay = ReplayHarness.Create();
        var full = await CatalogFixtures.DatasetsAsync(
            replay,
            CatalogFixtures.Latest,
            CatalogFixtures.English);
        return full with
        {
            Champions = full.Champions with
            {
                Entries = [.. full.Champions.Entries
                    .Where(static champion => Champions.Contains(champion.Summary.Id))],
            },
            Items = full.Items with
            {
                Entries = [.. full.Items.Entries.Where(static item => Items.Contains(item.Id))],
            },
            Runes = full.Runes with
            {
                Entries = [.. full.Runes.Entries.Where(static tree => RuneTrees.Contains(tree.Id))],
            },
            Summoners = full.Summoners with
            {
                Entries = [.. full.Summoners.Entries
                    .Where(static spell => Summoners.Contains(spell.Id))],
            },
        };
    }

    /// <summary>The datasets without the item <paramref name="id"/>.</summary>
    public static VersionDatasets WithoutItem(this VersionDatasets datasets, string id) =>
        datasets with
        {
            Items = datasets.Items with
            {
                Entries = [.. datasets.Items.Entries.Where(item => item.Id != id)],
            },
        };

    /// <summary>The datasets with the item <paramref name="id"/> changed.</summary>
    public static VersionDatasets ChangingItem(
        this VersionDatasets datasets,
        string id,
        Func<Item, Item> change) =>
        datasets with
        {
            Items = datasets.Items with
            {
                Entries = [.. datasets.Items.Entries
                    .Select(item => item.Id == id ? change(item) : item)],
            },
        };

    /// <summary>The datasets with the champion <paramref name="id"/> under another id.</summary>
    public static VersionDatasets RenamingChampion(
        this VersionDatasets datasets,
        string id,
        string renamed) =>
        datasets with
        {
            Champions = datasets.Champions with
            {
                Entries = [.. datasets.Champions.Entries.Select(champion =>
                    champion.Summary.Id == id
                        ? champion with { Summary = champion.Summary with { Id = renamed } }
                        : champion)],
            },
        };

    /// <summary>
    /// The datasets with no rune path at all, as Data Dragon before 7.22.1: the dataset is
    /// stored empty, without a content language.
    /// </summary>
    public static VersionDatasets WithoutRunes(this VersionDatasets datasets) =>
        datasets with { Runes = datasets.Runes with { Entries = [], ContentLanguage = null } };

    /// <summary>The datasets of a version Data Dragon does not ship: all four absent.</summary>
    public static VersionDatasets WithoutAnything(this VersionDatasets datasets) =>
        datasets.WithoutRunes() with
        {
            Champions = datasets.Champions with { Entries = [], ContentLanguage = null },
            Items = datasets.Items with { Entries = [], ContentLanguage = null },
            Summoners = datasets.Summoners with { Entries = [], ContentLanguage = null },
        };
}
