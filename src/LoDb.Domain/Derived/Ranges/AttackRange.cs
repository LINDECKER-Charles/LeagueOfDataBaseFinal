using LoDb.Domain.Catalog.Champions;

namespace LoDb.Domain.Derived.Ranges;

/// <summary>
/// Melee or ranged, from a champion's base attack range (UP 13).
/// </summary>
/// <remarks>
/// The ranges are bimodal but not cleanly: Nilah 225, Rakan 300 and Lillia 325 are melee,
/// Urgot 350 is ranged (Riot's own classification), hence a 325/350 cut rather than the
/// 200/425 gap.
/// </remarks>
public static class AttackRange
{
    /// <summary>Longest attack range still classified as melee.</summary>
    public const double MeleeMaxRange = 325;

    /// <summary>Data Dragon key of the base attack range in a champion's stats.</summary>
    public const string StatKey = "attackrange";

    /// <summary>The class, or <see langword="null"/> when the version ships no range.</summary>
    public static AttackRangeClass? ClassOf(ChampionSummary champion)
    {
        ArgumentNullException.ThrowIfNull(champion);
        if (!champion.Stats.TryGetValue(StatKey, out var range))
        {
            return null;
        }

        return range <= MeleeMaxRange ? AttackRangeClass.Melee : AttackRangeClass.Ranged;
    }
}
