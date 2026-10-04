namespace LoDb.Domain.Derived.Stats;

/// <summary>
/// The 12 classic item stat keys the site reads, in display order (UP 13).
/// </summary>
/// <remarks>
/// Data Dragon's <c>stats</c> block only carries these historical keys; every other effect
/// lives in the description. Percent keys hold fractions, and crit chance uses a
/// <c>Flat…</c> key although it is a percentage.
/// </remarks>
public static class ItemStats
{
    /// <summary>Every key read, in display order: what a stat facet enumerates.</summary>
    public static IReadOnlyList<ItemStatKey> Keys { get; } =
    [
        Flat("FlatPhysicalDamageMod", GameStat.AttackDamage),
        Flat("FlatMagicDamageMod", GameStat.AbilityPower),
        Percent("PercentAttackSpeedMod", GameStat.AttackSpeed),
        Percent("FlatCritChanceMod", GameStat.CritChance),
        Percent("PercentLifeStealMod", GameStat.LifeSteal),
        Flat("FlatHPPoolMod", GameStat.Health),
        Flat("FlatHPRegenMod", GameStat.HealthRegen),
        Flat("FlatArmorMod", GameStat.Armor),
        Flat("FlatSpellBlockMod", GameStat.MagicResist),
        Flat("FlatMPPoolMod", GameStat.Mana),
        Flat("FlatMovementSpeedMod", GameStat.MoveSpeed),
        Percent("PercentMovementSpeedMod", GameStat.MoveSpeed),
    ];

    /// <summary>Non-zero rows of a raw stat block, in display order.</summary>
    public static IReadOnlyList<ItemStat> Of(IReadOnlyDictionary<string, double>? stats)
    {
        if (stats is null || stats.Count == 0)
        {
            return [];
        }

        return [.. Keys
            .Where(key => stats.GetValueOrDefault(key.DdragonKey) != 0)
            .Select(key => new ItemStat
            {
                Stat = key.Stat,
                IsPercent = key.IsPercent,
                Value = stats[key.DdragonKey],
            })];
    }

    private static ItemStatKey Flat(string ddragonKey, GameStat stat) =>
        new() { DdragonKey = ddragonKey, Stat = stat, IsPercent = false };

    private static ItemStatKey Percent(string ddragonKey, GameStat stat) =>
        new() { DdragonKey = ddragonKey, Stat = stat, IsPercent = true };
}
