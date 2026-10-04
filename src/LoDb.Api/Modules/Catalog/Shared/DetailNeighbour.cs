using System.Globalization;
using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Catalog.Runes;
using LoDb.Domain.Catalog.Summoners;
using LoDb.Domain.Editions;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Shared;

/// <summary>An entry beside a detail page in its list's order, as the pager links it.</summary>
internal sealed record DetailNeighbour
{
    /// <summary>The id of its page: MonkeyKing, 3031, 8100, SummonerFlash.</summary>
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Its page, such as <c>items/1036-long-sword</c>, under the locale prefix.</summary>
    public required string CanonicalPath { get; init; }

    /// <summary>
    /// Which game it belongs to: walking the Classic range of the items or the spells, every
    /// name repeats a current entry's (UP 6). Champions and rune paths have no Classic twin:
    /// theirs is modern, as the legacy pager assumed.
    /// </summary>
    /// <remarks>
    /// Never null: a nullable use of the enum met first would make the generated component
    /// nullable for every other schema that references it.
    /// </remarks>
    public required Edition Edition { get; init; }

    public static DetailNeighbour Of(ChampionDetail champion, CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(champion);
        ArgumentNullException.ThrowIfNull(catalog);
        return new DetailNeighbour
        {
            Id = champion.Summary.Id,
            Name = champion.Summary.Name,
            CanonicalPath = catalog.PathOf(champion).Value,
            Edition = Edition.Modern,
        };
    }

    public static DetailNeighbour Of(Item item, CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(catalog);
        return new DetailNeighbour
        {
            Id = item.Id,
            Name = item.Name,
            CanonicalPath = catalog.PathOf(item).Value,
            Edition = item.Edition,
        };
    }

    public static DetailNeighbour Of(SummonerSpell spell, CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(spell);
        ArgumentNullException.ThrowIfNull(catalog);
        return new DetailNeighbour
        {
            Id = spell.Id,
            Name = spell.Name,
            CanonicalPath = catalog.PathOf(spell).Value,
            Edition = spell.Edition,
        };
    }

    public static DetailNeighbour Of(RuneTree tree, CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(catalog);
        return new DetailNeighbour
        {
            Id = tree.Id.ToString(CultureInfo.InvariantCulture),
            Name = tree.Name,
            CanonicalPath = catalog.PathOf(tree).Value,
            Edition = Edition.Modern,
        };
    }
}
