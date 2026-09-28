using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Domain.Catalog.Items;

namespace LoDb.Api.Modules.Catalog.Pickers.Options;

/// <summary>An item the build editor offers in a mode.</summary>
internal sealed record ItemOption
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required CatalogImage Image { get; init; }

    /// <summary>Total cost, the figure a build sums.</summary>
    public required int Gold { get; init; }

    /// <summary>Raw Data Dragon tags, the picker's filter.</summary>
    public required IReadOnlyList<string> Tags { get; init; }

    public static ItemOption Of(Item item, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(images);
        return new ItemOption
        {
            Id = item.Id,
            Name = item.Name,
            Image = images.Of(EntityImages.Icon(item)),
            Gold = item.Gold.Total,
            Tags = item.Tags,
        };
    }
}
