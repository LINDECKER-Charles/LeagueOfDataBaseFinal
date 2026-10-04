using LoDb.Domain.Catalog.Runes;

namespace LoDb.Domain.Builds.Rules;

/// <summary>
/// The rune trees of a patch reduced to what the rules ask: which perk sits in which slot of
/// which tree, and which ids the patch knows at all.
/// </summary>
public sealed class RuneTreeIndex
{
    private readonly Dictionary<int, IReadOnlyList<HashSet<int>>> _slotsByTree;
    private readonly HashSet<int> _knownIds;

    private RuneTreeIndex(
        Dictionary<int, IReadOnlyList<HashSet<int>>> slotsByTree,
        HashSet<int> knownIds)
    {
        _slotsByTree = slotsByTree;
        _knownIds = knownIds;
    }

    public static RuneTreeIndex Of(IEnumerable<RuneTree> trees)
    {
        ArgumentNullException.ThrowIfNull(trees);
        var slotsByTree = new Dictionary<int, IReadOnlyList<HashSet<int>>>();
        var knownIds = new HashSet<int>();
        foreach (var tree in trees)
        {
            var slots = SlotsOf(tree);
            knownIds.Add(tree.Id);
            knownIds.UnionWith(slots.SelectMany(static perks => perks));

            // A tree listed twice answers with its last listing, as the legacy index did.
            slotsByTree[tree.Id] = slots;
        }

        return new RuneTreeIndex(slotsByTree, knownIds);
    }

    public bool HasTree(int treeId) => _slotsByTree.ContainsKey(treeId);

    /// <summary>Whether <paramref name="perkId"/> is a perk of that slot of that tree.</summary>
    public bool IsInSlot(int treeId, int slotIndex, int perkId) =>
        _slotsByTree.TryGetValue(treeId, out var slots)
        && slotIndex >= 0
        && slotIndex < slots.Count
        && slots[slotIndex].Contains(perkId);

    /// <summary>
    /// The minor row of <paramref name="perkId"/> in the tree, keystone row excluded; null
    /// when the tree has no such minor.
    /// </summary>
    public int? MinorSlotOf(int treeId, int perkId)
    {
        if (!_slotsByTree.TryGetValue(treeId, out var slots))
        {
            return null;
        }

        for (var slot = BuildLimits.FirstMinorSlot; slot < slots.Count; slot++)
        {
            if (slots[slot].Contains(perkId))
            {
                return slot;
            }
        }

        return null;
    }

    /// <summary>Whether the patch knows <paramref name="id"/> as a style or a perk.</summary>
    public bool Knows(int id) => _knownIds.Contains(id);

    private static IReadOnlyList<HashSet<int>> SlotsOf(RuneTree tree) =>
        [.. tree.Slots.Select(static slot => slot.Runes.Select(static rune => rune.Id))
            .Select(static perks => perks.ToHashSet())];
}
