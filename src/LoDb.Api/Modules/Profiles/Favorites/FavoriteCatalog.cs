using System.Globalization;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Profiles.Favorites;

/// <summary>
/// Finds a favorite in a catalog, as the legacy picker did: a champion, an item or a summoner
/// spell by id, and a rune by the id of a rune or of a whole rune path.
/// </summary>
/// <remarks>
/// No mode filter: any item or spell of the patch is a valid favorite, Classic ones included.
/// Ids compare ordinally, as the datasets key them; a rune id must be written as the dataset
/// writes it, so <c>08112</c> finds nothing.
/// </remarks>
internal static class FavoriteCatalog
{
    /// <summary>
    /// The entry <paramref name="id"/> names in the slot; null when the patch lacks it.
    /// </summary>
    public static FavoriteEntry? Find(CatalogSnapshot catalog, FavoriteSlot slot, string id)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(id);
        return slot switch
        {
            FavoriteSlot.Champion => Champion(catalog, id),
            FavoriteSlot.Item => Item(catalog, id),
            FavoriteSlot.Rune => Rune(catalog, id),
            FavoriteSlot.Summoner => Summoner(catalog, id),
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
        };
    }

    private static FavoriteEntry? Champion(CatalogSnapshot catalog, string id) =>
        catalog.Champions.Find(id) is { } champion
            ? new FavoriteEntry(id, champion.Summary.Name, EntityImages.Portrait(champion))
            : null;

    private static FavoriteEntry? Item(CatalogSnapshot catalog, string id) =>
        catalog.Items.Find(id) is { } item
            ? new FavoriteEntry(id, item.Name, EntityImages.Icon(item))
            : null;

    private static FavoriteEntry? Summoner(CatalogSnapshot catalog, string id) =>
        catalog.Summoners.Find(id) is { } spell
            ? new FavoriteEntry(id, spell.Name, EntityImages.Icon(spell))
            : null;

    private static FavoriteEntry? Rune(CatalogSnapshot catalog, string id)
    {
        if (catalog.Runes.Find(id) is { } tree)
        {
            return new FavoriteEntry(id, tree.Name, EntityImages.Icon(tree));
        }

        return IsCanonicalNumber(id, out var runeId) && catalog.FindRune(runeId) is { } found
            ? new FavoriteEntry(id, found.Rune.Name, EntityImages.Icon(found.Rune))
            : null;
    }

    private static bool IsCanonicalNumber(string id, out int number) =>
        int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out number)
        && number.ToString(CultureInfo.InvariantCulture) == id;
}
