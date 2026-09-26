using LoDb.Domain.Builds.Structures;

namespace LoDb.Domain.Builds.Rules;

/// <summary>
/// The rules of a rune page: a primary tree with one pick per slot, the keystone first, and
/// another tree with two picks from distinct minor rows.
/// </summary>
/// <remarks>
/// An unknown style stops the checks of its side: its picks cannot be placed in any slot.
/// </remarks>
internal static class RunePageRules
{
    public static IEnumerable<string> Errors(RunePageInput? runes, RuneTreeIndex trees)
    {
        if (runes is null)
        {
            return [BuildErrors.StructureInvalid];
        }

        return [.. PrimaryErrors(runes, trees), .. SecondaryErrors(runes, trees)];
    }

    private static IEnumerable<string> PrimaryErrors(RunePageInput runes, RuneTreeIndex trees)
    {
        if (runes.PrimaryStyleId is not { } style || !trees.HasTree(style))
        {
            return [BuildErrors.PrimaryStyle];
        }

        var picks = runes.PrimarySelections;
        if (picks is null || picks.Count != BuildLimits.PrimaryPicks)
        {
            return [BuildErrors.PrimaryCount];
        }

        // Pick i must be a perk of slot i of the primary tree, slot 0 being the keystone.
        return picks
            .Where((pick, slot) => pick is not { } perk || !trees.IsInSlot(style, slot, perk))
            .Select(static _ => BuildErrors.PrimarySlot);
    }

    private static List<string> SecondaryErrors(RunePageInput runes, RuneTreeIndex trees)
    {
        if (runes.SecondaryStyleId is not { } style || !trees.HasTree(style))
        {
            return [BuildErrors.SecondaryStyle];
        }

        if (style == runes.PrimaryStyleId)
        {
            return [BuildErrors.SecondarySameStyle];
        }

        var picks = runes.SecondarySelections;
        if (picks is null || picks.Count != BuildLimits.SecondaryPicks)
        {
            return [BuildErrors.SecondaryCount];
        }

        return SecondaryPickErrors(style, picks, trees);
    }

    private static List<string> SecondaryPickErrors(
        int style,
        IReadOnlyList<int?> picks,
        RuneTreeIndex trees)
    {
        var errors = new List<string>();
        var usedRows = new HashSet<int>();
        foreach (var pick in picks)
        {
            if ((pick is { } perk ? trees.MinorSlotOf(style, perk) : null) is not { } row)
            {
                errors.Add(BuildErrors.SecondarySlot);
            }
            else if (!usedRows.Add(row))
            {
                errors.Add(BuildErrors.SecondarySameSlot);
            }
        }

        return errors;
    }
}
