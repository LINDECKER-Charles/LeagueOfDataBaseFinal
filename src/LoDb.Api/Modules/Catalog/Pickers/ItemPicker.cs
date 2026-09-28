using LoDb.Api.Modules.Catalog.Pickers.Options;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Catalog.Modes;
using LoDb.Domain.Derived.Items;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Pickers;

/// <summary>
/// <c>GET /api/pickers/items</c>: the items a build of the mode may carry, by name. Classic
/// items, those the mode's map lacks, unpurchasable, hidden and champion-bound ones are left
/// out (<see cref="ItemPlayability.IsPickable"/>).
/// </summary>
internal sealed record ItemPicker
{
    public required string Version { get; init; }

    public required string Language { get; init; }

    /// <summary>The mode served: an absent or unknown one falls back to the default.</summary>
    public required GameMode Mode { get; init; }

    public required IReadOnlyList<ItemOption> Options { get; init; }

    public static IReadOnlyList<Item> Pickable(CatalogSnapshot catalog, GameMode mode)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return [.. catalog.Items.Entries.Where(item => ItemPlayability.IsPickable(item, mode))];
    }

    public static ItemPicker Of(CatalogSnapshot catalog, GameMode mode, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return new ItemPicker
        {
            Version = catalog.Version.Value,
            Language = catalog.Language.Code,
            Mode = mode,
            Options = [.. Pickable(catalog, mode)
                .Select(item => ItemOption.Of(item, images))
                .OrderBy(static option => option.Name, StringComparer.Ordinal)],
        };
    }
}
