using System.Globalization;
using LoDb.Domain.Catalog.Champions;

namespace LoDb.Domain.Derived.Champions;

/// <summary>
/// Removes the chromas Data Dragon lists as skins of their own (UP 9).
/// </summary>
/// <remarks>
/// Data Dragon inlines each chroma as a skin ("Popstar Ahri (Amethyst)"), dozens per modern
/// champion, none with a splash of its own. They carry the id of the CommunityDragon chroma
/// their parent skin already shows, so any skin whose id is a known chroma goes. Without
/// chroma data the list is kept whole rather than guessed from names.
/// </remarks>
public static class ChromaSkins
{
    public static IReadOnlyList<Skin> Without(IReadOnlyList<Skin> skins)
    {
        ArgumentNullException.ThrowIfNull(skins);
        var chromaIds = skins
            .SelectMany(skin => skin.Chromas)
            .Select(chroma => chroma.Id)
            .ToHashSet();

        return chromaIds.Count == 0 ? skins : [.. skins.Where(skin => !IsChroma(skin, chromaIds))];
    }

    private static bool IsChroma(Skin skin, HashSet<int> chromaIds) =>
        int.TryParse(skin.Id, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
        && chromaIds.Contains(id);
}
