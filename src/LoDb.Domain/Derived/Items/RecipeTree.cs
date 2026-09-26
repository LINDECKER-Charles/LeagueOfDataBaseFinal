using System.Collections.Immutable;
using LoDb.Domain.Catalog.Items;

namespace LoDb.Domain.Derived.Items;

/// <summary>
/// The top-down recipe of an item: the item at the root, every component expanded down to the
/// base items.
/// </summary>
/// <remarks>
/// The cycle guard is per path, so the same component may appear in sibling branches (two
/// Long Swords), and <see cref="MaxDepth"/> bounds pathological recipes. Components the
/// dataset no longer carries are dropped.
/// </remarks>
public static class RecipeTree
{
    /// <summary>Deepest level expanded below the root.</summary>
    public const int MaxDepth = 6;

    /// <summary>The tree, or <see langword="null"/> when the dataset lacks the root.</summary>
    public static RecipeNode? Build(string rootId, IReadOnlyDictionary<string, Item> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return Node(rootId, items, ImmutableHashSet.Create<string>(StringComparer.Ordinal));
    }

    // The ancestors are the path from the root, so their count is the depth of the node.
    private static RecipeNode? Node(
        string id,
        IReadOnlyDictionary<string, Item> items,
        ImmutableHashSet<string> ancestors)
    {
        if (ancestors.Contains(id)
            || ancestors.Count > MaxDepth
            || !items.TryGetValue(id, out var item))
        {
            return null;
        }

        var path = ancestors.Add(id);
        return new RecipeNode
        {
            Id = item.Id,
            Name = item.Name,
            Image = item.Image,
            Gold = item.Gold.Total,
            Combine = item.Gold.Base,
            Children = [.. item.From
                .Select(componentId => Node(componentId, items, path))
                .OfType<RecipeNode>()],
        };
    }
}
