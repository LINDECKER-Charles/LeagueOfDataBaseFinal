using LoDb.Domain.Builds.Rules;
using LoDb.Domain.Builds.Structures;

namespace LoDb.Domain.Builds.Editing;

/// <summary>
/// The editor's moves on the steps of a purchase order, bounded as the server checks them:
/// 10 steps at most. A refused move returns the list it was given.
/// </summary>
public static class StepList
{
    public static BuildStep CreateStep(string label = "") =>
        new() { Label = label, Note = null, Items = [] };

    public static int TotalItems(IReadOnlyList<BuildStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        return steps.Sum(static step => step.Items.Count);
    }

    public static bool CanAddStep(IReadOnlyList<BuildStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        return steps.Count < BuildLimits.StepsMax;
    }

    public static IReadOnlyList<BuildStep> AddStep(
        IReadOnlyList<BuildStep> steps,
        string label = "") =>
        CanAddStep(steps) ? [.. steps, CreateStep(label)] : steps;

    public static IReadOnlyList<BuildStep> RemoveStep(IReadOnlyList<BuildStep> steps, int index)
    {
        ArgumentNullException.ThrowIfNull(steps);
        return ListMoves.IsIndex(steps, index)
            ? [.. steps.Where((_, position) => position != index)]
            : steps;
    }

    /// <summary>Moves a step by <paramref name="delta"/> places, never past either end.</summary>
    public static IReadOnlyList<BuildStep> MoveStep(
        IReadOnlyList<BuildStep> steps,
        int index,
        int delta)
    {
        ArgumentNullException.ThrowIfNull(steps);
        return ListMoves.ByDelta(steps, index, delta);
    }

    /// <summary>Drops a step at an insertion point, counted before it left its place.</summary>
    public static IReadOnlyList<BuildStep> MoveStepToIndex(
        IReadOnlyList<BuildStep> steps,
        int fromIndex,
        int insertIndex)
    {
        ArgumentNullException.ThrowIfNull(steps);
        return ListMoves.ToInsertionPoint(steps, fromIndex, insertIndex);
    }

    /// <summary>
    /// Where an entry dropped at <paramref name="insertIndex"/> lands, the position a screen
    /// reader announces.
    /// </summary>
    public static int RestingIndex(int from, int insertIndex, int length) =>
        ListMoves.RestingIndex(from, insertIndex, length);
}
