using LoDb.Domain.Builds.Rules;
using LoDb.Domain.Builds.Structures;

namespace LoDb.Domain.Builds.Editing;

/// <summary>
/// The editor's moves on the items of the steps, bounded as the server checks them: 8 items
/// per step and 40 in all, duplicates allowed. A refused move returns the list it was given.
/// </summary>
/// <remarks>
/// A move may empty a step: the one-item minimum is the server's check at submission.
/// </remarks>
public static class StepItems
{
    public static bool CanAddItem(IReadOnlyList<BuildStep> steps, int stepIndex)
    {
        ArgumentNullException.ThrowIfNull(steps);
        return ListMoves.IsIndex(steps, stepIndex)
            && steps[stepIndex].Items.Count < BuildLimits.ItemsPerStepMax
            && StepList.TotalItems(steps) < BuildLimits.TotalItemsMax;
    }

    /// <summary>Appends an item to a step.</summary>
    public static IReadOnlyList<BuildStep> AddItem(
        IReadOnlyList<BuildStep> steps,
        int stepIndex,
        string itemId)
    {
        ArgumentNullException.ThrowIfNull(steps);
        var tail = ListMoves.IsIndex(steps, stepIndex) ? steps[stepIndex].Items.Count : 0;
        return InsertItem(steps, new ItemLocation(stepIndex, tail), itemId);
    }

    /// <summary>Inserts an item at a position clamped to its step.</summary>
    public static IReadOnlyList<BuildStep> InsertItem(
        IReadOnlyList<BuildStep> steps,
        ItemLocation at,
        string itemId)
    {
        ArgumentNullException.ThrowIfNull(at);
        return CanAddItem(steps, at.Step)
            ? WithItems(steps, at.Step, ListMoves.InsertAt(steps[at.Step].Items, at.Index, itemId))
            : steps;
    }

    public static IReadOnlyList<BuildStep> RemoveItem(
        IReadOnlyList<BuildStep> steps,
        ItemLocation at)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(at);
        if (!ListMoves.IsIndex(steps, at.Step)
            || !ListMoves.IsIndex(steps[at.Step].Items, at.Index))
        {
            return steps;
        }

        IReadOnlyList<string> items = [.. steps[at.Step].Items.Where((_, i) => i != at.Index)];
        return WithItems(steps, at.Step, items);
    }

    /// <summary>Moves an item within its step by <paramref name="delta"/> places.</summary>
    public static IReadOnlyList<BuildStep> MoveItem(
        IReadOnlyList<BuildStep> steps,
        ItemLocation from,
        int delta)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(from);
        return ListMoves.IsIndex(steps, from.Step)
            ? Replaced(
                steps,
                from.Step,
                ListMoves.ByDelta(steps[from.Step].Items, from.Index, delta))
            : steps;
    }

    /// <summary>Drops an item at an insertion point of its own step.</summary>
    public static IReadOnlyList<BuildStep> MoveItemToIndex(
        IReadOnlyList<BuildStep> steps,
        ItemLocation from,
        int insertIndex)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(from);
        return ListMoves.IsIndex(steps, from.Step)
            ? Replaced(
                steps,
                from.Step,
                ListMoves.ToInsertionPoint(steps[from.Step].Items, from.Index, insertIndex))
            : steps;
    }

    /// <summary>
    /// Moves an item to an insertion point of another step, refused when that step is full;
    /// within one step it is a reorder.
    /// </summary>
    public static IReadOnlyList<BuildStep> TransferItem(
        IReadOnlyList<BuildStep> steps,
        ItemLocation from,
        ItemLocation to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        if (from.Step == to.Step)
        {
            return MoveItemToIndex(steps, from, to.Index);
        }

        if (!ListMoves.IsIndex(steps, from.Step)
            || !ListMoves.IsIndex(steps, to.Step)
            || !ListMoves.IsIndex(steps[from.Step].Items, from.Index)
            || steps[to.Step].Items.Count >= BuildLimits.ItemsPerStepMax)
        {
            return steps;
        }

        var itemId = steps[from.Step].Items[from.Index];
        var moved = RemoveItem(steps, from);
        var landed = ListMoves.InsertAt(moved[to.Step].Items, to.Index, itemId);
        return WithItems(moved, to.Step, landed);
    }

    // Keeps the very list when the items did not change, so a no-op stays one.
    private static IReadOnlyList<BuildStep> Replaced(
        IReadOnlyList<BuildStep> steps,
        int stepIndex,
        IReadOnlyList<string> items) =>
        ReferenceEquals(items, steps[stepIndex].Items)
            ? steps
            : WithItems(steps, stepIndex, items);

    private static BuildStep[] WithItems(
        IReadOnlyList<BuildStep> steps,
        int stepIndex,
        IReadOnlyList<string> items) =>
        [.. steps.Select((step, i) => i == stepIndex ? step with { Items = items } : step)];
}
