using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Catalog.Modes;
using LoDb.Domain.Editions;

namespace LoDb.Domain.Derived.Items;

/// <summary>
/// Whether an item can be played, and offered, in a build mode.
/// </summary>
/// <remarks>
/// Every build mode is a current-game queue, so the LoL Classic catalogue is never playable
/// there, although item.json flags most classic items on ARAM. A missing map flag excludes
/// nothing: older versions predate some maps and must not blank the whole catalogue.
/// </remarks>
public static class ItemPlayability
{
    /// <summary>Whether a build in the mode may carry the item.</summary>
    public static bool IsAvailableOn(Item item, GameMode mode)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Edition == Edition.Modern
            && item.Maps.GetValueOrDefault((int)GameModes.MapOf(mode), true);
    }

    /// <summary>
    /// Whether the item picker offers the item: purchasable, available in the mode, shown in
    /// the shop and not bound to one champion.
    /// </summary>
    public static bool IsPickable(Item item, GameMode mode) =>
        IsAvailableOn(item, mode)
        && item.Gold.IsPurchasable
        && !item.IsHiddenFromAll
        && string.IsNullOrEmpty(item.RequiredChampion);

    /// <summary>
    /// Labels of the items a build cannot carry in the mode, the readable part of the error:
    /// one per item, order kept, classic items qualified by their id.
    /// </summary>
    public static IReadOnlyList<string> UnavailableNames(IEnumerable<Item> items, GameMode mode)
    {
        ArgumentNullException.ThrowIfNull(items);
        return [.. items
            .Where(item => !IsAvailableOn(item, mode))
            .Select(item => ItemEdition.QualifiedName(item.Id, item.Name))
            .Distinct(StringComparer.Ordinal)];
    }
}
