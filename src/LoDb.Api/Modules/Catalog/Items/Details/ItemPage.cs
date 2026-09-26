using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Derived.Items;
using LoDb.Ingestion.Catalog.Snapshots;
using LoDb.Ingestion.Images;

namespace LoDb.Api.Modules.Catalog.Items.Details;

/// <summary>
/// Everything an item's page shows, looked up once so that its images resolve in one query.
/// </summary>
internal sealed record ItemPage
{
    public required CatalogSnapshot Catalog { get; init; }

    public required Item Item { get; init; }

    /// <summary>The item and its components, recursively (debris included).</summary>
    public RecipeNode? Recipe { get; init; }

    /// <summary>The listed items it builds into, in the upstream order.</summary>
    public required IReadOnlyList<Item> Upgrades { get; init; }

    public ChampionDetail? RequiredChampion { get; init; }

    public ChampionDetail? RequiredAlly { get; init; }

    public static ItemPage Of(CatalogSnapshot catalog, Item item)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(item);
        var items = catalog.Items.Entries
            .DistinctBy(static entry => entry.Id)
            .ToDictionary(static entry => entry.Id, StringComparer.Ordinal);
        return new ItemPage
        {
            Catalog = catalog,
            Item = item,
            Recipe = RecipeTree.Build(item.Id, items),
            Upgrades = [.. item.Into
                .Distinct(StringComparer.Ordinal)
                .Select(catalog.Items.Find)
                .OfType<Item>()
                .Where(catalog.IsListed)],
            RequiredChampion = ChampionNamed(catalog, item.RequiredChampion),
            RequiredAlly = ChampionNamed(catalog, item.RequiredAlly),
        };
    }

    /// <summary>Every image of the page, each file once or more.</summary>
    public IEnumerable<DdragonImage?> Images()
    {
        IEnumerable<DdragonImage?> recipe = Recipe is null
            ? []
            : Flatten(Recipe).Select(static node => EntityImages.ItemIcon(node.Image));
        IEnumerable<ChampionDetail> champions =
            new[] { RequiredChampion, RequiredAlly }.OfType<ChampionDetail>();
        return recipe
            .Append(EntityImages.Icon(Item))
            .Concat(Upgrades.Select(EntityImages.Icon))
            .Concat(champions.Select(EntityImages.Portrait));
    }

    // Data Dragon names the champion by id in its own casing ("Kalista", "Gangplank").
    private static ChampionDetail? ChampionNamed(CatalogSnapshot catalog, string? id) =>
        string.IsNullOrEmpty(id)
            ? null
            : catalog.Champions.Find(id) ?? catalog.Champions.Entries.FirstOrDefault(champion =>
                string.Equals(champion.Summary.Id, id, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<RecipeNode> Flatten(RecipeNode node) =>
        node.Children.SelectMany(Flatten).Prepend(node);
}
