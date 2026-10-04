using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Catalog.Runes;

namespace LoDb.Domain.Builds.Rules;

/// <summary>
/// One patch as the build rules read it: its champion ids, its rune trees and its items.
/// </summary>
public sealed class BuildCatalog
{
    private readonly HashSet<string> _championIds;
    private readonly Dictionary<string, Item> _itemsById;

    /// <param name="championIds">The champion ids of the patch.</param>
    /// <param name="runeTrees">The rune trees, empty for a patch before runes reforged.</param>
    /// <param name="items">
    /// The items in dataset order; the first entry of an id wins, as the dataset map keys it.
    /// </param>
    public BuildCatalog(
        IEnumerable<string> championIds,
        IEnumerable<RuneTree> runeTrees,
        IEnumerable<Item> items)
    {
        ArgumentNullException.ThrowIfNull(championIds);
        ArgumentNullException.ThrowIfNull(items);
        _championIds = new HashSet<string>(championIds, StringComparer.Ordinal);
        Runes = RuneTreeIndex.Of(runeTrees);
        _itemsById = new Dictionary<string, Item>(StringComparer.Ordinal);
        var ordered = new List<Item>();
        foreach (var item in items)
        {
            if (_itemsById.TryAdd(item.Id, item))
            {
                ordered.Add(item);
            }
        }

        Items = ordered;
    }

    public RuneTreeIndex Runes { get; }

    /// <summary>Every item of the patch, once each, in dataset order.</summary>
    public IReadOnlyList<Item> Items { get; }

    public bool HasChampion(string championId) => _championIds.Contains(championId);

    public Item? FindItem(string itemId) => _itemsById.GetValueOrDefault(itemId);
}
