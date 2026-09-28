using LoDb.Domain.Catalog.Items;

namespace LoDb.Domain.Derived.Items;

/// <summary>
/// Tier of an item from its recipe depth and what it builds into (UP 13).
/// </summary>
/// <remarks>
/// Data Dragon has no tier field. A base item has no depth: it is a component when it builds
/// into something, and has no tier otherwise (consumables, trinkets). Depth 2 is an epic,
/// anything deeper a legendary.
/// </remarks>
public static class ItemTiers
{
    /// <summary>Deepest recipe level of an epic item.</summary>
    public const int EpicMaxDepth = 2;

    public static ItemTier? Of(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Depth is not { } depth)
        {
            return item.Into.Count > 0 ? ItemTier.Component : null;
        }

        return depth <= EpicMaxDepth ? ItemTier.Epic : ItemTier.Legendary;
    }
}
