using LoDb.Domain.Builds.Rules;
using LoDb.Domain.Builds.Structures;

namespace LoDb.Domain.Builds.Editing;

/// <summary>
/// The rune board's rules, those of the game client: one perk per primary slot, a secondary
/// tree other than the primary, and two secondary picks from distinct minor rows, a third
/// one evicting the oldest.
/// </summary>
/// <remarks>
/// A refused move returns the very draft it was given, so a caller tells a no-op by identity.
/// </remarks>
public static class RuneDraftRules
{
    /// <summary>The row of a stored secondary perk the patch no longer places.</summary>
    public const int GhostSlot = -1;

    public static RuneDraft Empty { get; } = new()
    {
        PrimaryPerks = EmptyPrimary(),
        SecondaryPicks = [],
    };

    /// <summary>
    /// Switches the primary tree, which empties its slots; taking the secondary tree as
    /// primary also clears the secondary side.
    /// </summary>
    public static RuneDraft SelectPrimaryStyle(RuneDraft draft, int styleId)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.PrimaryStyleId == styleId)
        {
            return draft;
        }

        var collides = draft.SecondaryStyleId == styleId;
        return new RuneDraft
        {
            PrimaryStyleId = styleId,
            PrimaryPerks = EmptyPrimary(),
            SecondaryStyleId = collides ? null : draft.SecondaryStyleId,
            SecondaryPicks = collides ? [] : draft.SecondaryPicks,
        };
    }

    public static RuneDraft SelectPrimaryPerk(RuneDraft draft, int slotIndex, int perkId)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (slotIndex is < 0 or >= BuildLimits.PrimaryPicks)
        {
            return draft;
        }

        List<int?> perks = [.. draft.PrimaryPerks];
        perks[slotIndex] = perkId;
        return draft with { PrimaryPerks = perks };
    }

    /// <summary>Switches the secondary tree, never to the primary one; its picks restart.</summary>
    public static RuneDraft SelectSecondaryStyle(RuneDraft draft, int styleId)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return styleId == draft.PrimaryStyleId || styleId == draft.SecondaryStyleId
            ? draft
            : draft with { SecondaryStyleId = styleId, SecondaryPicks = [] };
    }

    /// <summary>
    /// Picks a secondary perk: it replaces the pick of its row, else joins the picks, the
    /// oldest leaving once there are more than two. The keystone row is refused.
    /// </summary>
    public static RuneDraft SelectSecondaryPerk(RuneDraft draft, int slotIndex, int perkId)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (slotIndex == BuildLimits.KeystoneSlot)
        {
            return draft;
        }

        List<SecondaryPick> picks =
        [
            .. draft.SecondaryPicks.Where(pick => pick.SlotIndex != slotIndex),
            new SecondaryPick(slotIndex, perkId),
        ];
        var overflow = Math.Max(0, picks.Count - BuildLimits.SecondaryPicks);
        return draft with { SecondaryPicks = picks[overflow..] };
    }

    public static bool IsComplete(RuneDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return draft.PrimaryStyleId is not null
            && draft.SecondaryStyleId is not null
            && draft.PrimaryPerks.All(static perk => perk is not null)
            && draft.SecondaryPicks.Count == BuildLimits.SecondaryPicks;
    }

    /// <summary>The page to store: what was picked, nothing invented for the rest.</summary>
    public static RunePage ToRunes(RuneDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return new RunePage
        {
            PrimaryStyleId = draft.PrimaryStyleId ?? RunePage.Unset,
            PrimarySelections = [.. draft.PrimaryPerks.OfType<int>()],
            SecondaryStyleId = draft.SecondaryStyleId ?? RunePage.Unset,
            SecondarySelections = [.. draft.SecondaryPicks.Select(static pick => pick.PerkId)],
        };
    }

    /// <summary>
    /// The draft of a stored page, each secondary perk placed back in its row by
    /// <paramref name="slotOfSecondary"/> (style id, perk id), else kept visible as a ghost.
    /// </summary>
    public static RuneDraft FromRunes(RunePage runes, Func<int, int, int?> slotOfSecondary)
    {
        ArgumentNullException.ThrowIfNull(runes);
        ArgumentNullException.ThrowIfNull(slotOfSecondary);
        return new RuneDraft
        {
            PrimaryStyleId = StyleOf(runes.PrimaryStyleId),
            PrimaryPerks =
            [
                .. Enumerable.Range(0, BuildLimits.PrimaryPicks)
                    .Select(slot => slot < runes.PrimarySelections.Count
                        ? runes.PrimarySelections[slot]
                        : (int?)null),
            ],
            SecondaryStyleId = StyleOf(runes.SecondaryStyleId),
            SecondaryPicks =
            [
                .. runes.SecondarySelections.Take(BuildLimits.SecondaryPicks).Select(perk =>
                    new SecondaryPick(
                        slotOfSecondary(runes.SecondaryStyleId, perk) ?? GhostSlot,
                        perk)),
            ],
        };
    }

    private static int? StyleOf(int styleId) => styleId == RunePage.Unset ? null : styleId;

    private static int?[] EmptyPrimary() => new int?[BuildLimits.PrimaryPicks];
}
