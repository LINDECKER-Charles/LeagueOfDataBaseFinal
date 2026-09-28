using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Ingestion.Catalog.Snapshots;
using LoDb.Ingestion.Images;

namespace LoDb.Api.Modules.Profiles.Favorites.Views;

/// <summary>The four favorite slots of a profile, resolved on one patch.</summary>
internal sealed record FavoriteBoard
{
    public required FavoriteView Champion { get; init; }

    public required FavoriteView Item { get; init; }

    public required FavoriteView Rune { get; init; }

    public required FavoriteView Summoner { get; init; }

    /// <summary>
    /// The entries of <paramref name="ids"/> on the patch; none without a catalog.
    /// </summary>
    public static IReadOnlyDictionary<FavoriteSlot, FavoriteEntry> Find(
        FavoriteIds ids,
        CatalogSnapshot? catalog)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var entries = new Dictionary<FavoriteSlot, FavoriteEntry>();
        foreach (var slot in Enum.GetValues<FavoriteSlot>())
        {
            if (catalog is not null
                && ids.Get(slot) is { Length: > 0 } id
                && FavoriteCatalog.Find(catalog, slot, id) is { } entry)
            {
                entries[slot] = entry;
            }
        }

        return entries;
    }

    /// <summary>The images the entries show, to resolve along the answer's others.</summary>
    public static IEnumerable<DdragonImage?> Images(
        IReadOnlyDictionary<FavoriteSlot, FavoriteEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return entries.Values.Select(static entry => entry.Image);
    }

    public static FavoriteBoard Of(
        FavoriteIds ids,
        IReadOnlyDictionary<FavoriteSlot, FavoriteEntry> entries,
        ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(entries);
        return new FavoriteBoard
        {
            Champion = View(FavoriteSlot.Champion),
            Item = View(FavoriteSlot.Item),
            Rune = View(FavoriteSlot.Rune),
            Summoner = View(FavoriteSlot.Summoner),
        };

        FavoriteView View(FavoriteSlot slot) =>
            FavoriteView.Of(ids.Get(slot), entries.GetValueOrDefault(slot), images);
    }
}
