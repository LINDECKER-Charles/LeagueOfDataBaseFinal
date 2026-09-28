using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Editions;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Items.Details;

/// <summary>
/// An item the page's item builds into: the fields of an <see cref="EntityLink"/>, and the
/// price its card shows under the name, as the legacy recipe nodes did.
/// </summary>
internal sealed record ItemUpgrade
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Its page, such as <c>items/1036-long-sword</c>, under the locale prefix.</summary>
    public required string CanonicalPath { get; init; }

    public required CatalogImage Image { get; init; }

    /// <summary>Which game it belongs to; twins share their name.</summary>
    public required Edition Edition { get; init; }

    /// <summary>Total cost of the upgrade.</summary>
    public required int Gold { get; init; }

    public static ItemUpgrade Of(Item item, CatalogSnapshot catalog, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(images);
        return new ItemUpgrade
        {
            Id = item.Id,
            Name = item.Name,
            CanonicalPath = catalog.PathOf(item).Value,
            Image = images.Of(EntityImages.Icon(item)),
            Edition = item.Edition,
            Gold = item.Gold.Total,
        };
    }
}
