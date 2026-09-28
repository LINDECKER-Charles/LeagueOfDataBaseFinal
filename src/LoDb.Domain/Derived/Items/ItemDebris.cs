using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Text;

namespace LoDb.Domain.Derived.Items;

/// <summary>
/// Riot's data debris among items, cleaned in one place so that the list, the search, the
/// counts, the sitemap and the detail lookup all agree (UP 10).
/// </summary>
/// <remarks>
/// Unnamed entries (2008, 226660, 772139, 772140 on 16.16.1) and self-declared placeholders
/// (7050 "Gangplank Placeholder") are not encyclopedia entries. Data Dragon translates the
/// placeholder marker in some languages (ar_AE "نائب غانغ بلانك", zh_CN "普朗克 占位"), so it
/// is read in the en_US name of the same version, as the item type is. Marked-up
/// names are reduced to the name proper (3901-3903 ship
/// "&lt;rarityLegendary&gt;Feu à volonté&lt;/rarityLegendary&gt;&lt;br&gt;…"). Recipes still
/// read debris items: they are hidden from browsing, not removed from the dataset.
/// </remarks>
public static class ItemDebris
{
    private const string PlaceholderMarker = "Placeholder";

    /// <summary>
    /// The name to display and index: unresolved tokens removed, markup reduced to the text
    /// before the first line break.
    /// </summary>
    public static string DisplayName(string? rawName) =>
        DdragonText.PlainName(DdragonText.Clean(rawName));

    /// <summary>Whether the item is debris rather than an encyclopedia entry.</summary>
    /// <param name="item">The item, in any language: an empty name is read here.</param>
    /// <param name="englishName">
    /// The en_US name of the same id and version, where the placeholder marker is read;
    /// <see langword="null"/> when en_US lacks the id, the marker then read in the item's name.
    /// </param>
    public static bool IsDebris(Item item, string? englishName)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Name.Length == 0
            || (englishName ?? item.Name).Contains(PlaceholderMarker, StringComparison.Ordinal);
    }
}
