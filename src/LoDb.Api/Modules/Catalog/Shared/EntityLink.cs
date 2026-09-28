using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Editions;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Shared;

/// <summary>
/// Another entry a page links to: a recipe component, an upgrade, a bound champion.
/// </summary>
internal sealed record EntityLink
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Its page, such as <c>items/1036-long-sword</c>, under the locale prefix.</summary>
    public required string CanonicalPath { get; init; }

    public required CatalogImage Image { get; init; }

    /// <summary>For an item, which game it belongs to; twins share their name.</summary>
    public Edition? Edition { get; init; }

    public static EntityLink Of(Item item, CatalogSnapshot catalog, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(images);
        return new EntityLink
        {
            Id = item.Id,
            Name = item.Name,
            CanonicalPath = catalog.PathOf(item).Value,
            Image = images.Of(EntityImages.Icon(item)),
            Edition = item.Edition,
        };
    }

    public static EntityLink Of(
        ChampionDetail champion,
        CatalogSnapshot catalog,
        ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(champion);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(images);
        return new EntityLink
        {
            Id = champion.Summary.Id,
            Name = champion.Summary.Name,
            CanonicalPath = catalog.PathOf(champion).Value,
            Image = images.Of(EntityImages.Portrait(champion)),
        };
    }
}
