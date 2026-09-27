using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Catalog.Modes;
using LoDb.Domain.Derived.Items;
using LoDb.Domain.Derived.Stats;
using LoDb.Domain.Editions;
using LoDb.Domain.Text;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Items;

/// <summary>
/// An item as the list shows it: what it builds into, and the values its facets filter on:
/// tags, edition, maps, tier, price, stats.
/// </summary>
internal sealed record ItemCard
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string CanonicalPath { get; init; }

    public required CatalogImage Image { get; init; }

    /// <summary>Which game the item belongs to: 1004 and 771004 share a name.</summary>
    public required Edition Edition { get; init; }

    /// <summary>The same-named item of the other game, when there is one.</summary>
    public CounterpartLink? Counterpart { get; init; }

    /// <summary>
    /// The one-line description, or the full one when Data Dragon ships none (Emberknife),
    /// template tokens removed.
    /// </summary>
    public required string Summary { get; init; }

    public required ItemGold Gold { get; init; }

    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary>Data Dragon ids of the named maps the item is available on.</summary>
    public required IReadOnlyList<int> Maps { get; init; }

    public ItemTier? Tier { get; init; }

    public required bool Consumable { get; init; }

    public required IReadOnlyList<ItemStat> Stats { get; init; }

    /// <summary>
    /// Ids of the listed items it builds into, in the upstream order; the list names them in
    /// its <c>related</c> links, each once whatever the number of cards pointing to it.
    /// </summary>
    public required IReadOnlyList<string> Upgrades { get; init; }

    public static ItemCard Of(Item item, CatalogSnapshot catalog, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(images);
        return new ItemCard
        {
            Id = item.Id,
            Name = item.Name,
            CanonicalPath = catalog.PathOf(item).Value,
            Image = images.Of(EntityImages.Icon(item)),
            Edition = item.Edition,
            Counterpart = item.Counterpart is { } twin
                ? CounterpartLink.Of(twin, catalog.TwinOf(item) is { } held
                    ? catalog.PathOf(held).Value
                    : null)
                : null,
            Summary = DdragonText.Clean(
                string.IsNullOrWhiteSpace(item.Plaintext) ? item.Description : item.Plaintext),
            Gold = item.Gold,
            Tags = item.Tags,
            Maps = [.. GameMaps.AvailableOn(item.Maps).Select(static map => (int)map)],
            Tier = ItemTiers.Of(item),
            Consumable = item.IsConsumed,
            Stats = ItemStats.Of(item.Stats),
            Upgrades = [.. ItemUpgrades.Of(item, catalog).Select(static upgrade => upgrade.Id)],
        };
    }
}
