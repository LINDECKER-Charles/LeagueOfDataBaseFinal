using LoDb.Domain.Derived.Ranges;

namespace LoDb.Domain.Derived.Stats;

/// <summary>
/// The champion stat lines a page shows, in display order: the stat, the Data Dragon key of
/// its value at level 1, and that of its gain per level (null for the stats that do not grow).
/// </summary>
/// <remarks>
/// One table for the page and the fingerprint of a champion's state: a line the page does not
/// show (<c>crit</c>, <c>attackspeedoffset</c>…) never opens a state.
/// </remarks>
public static class ChampionStatRows
{
    public static IReadOnlyList<(GameStat Stat, string BaseKey, string? PerLevelKey)> All
    { get; } =
    [
        (GameStat.Health, "hp", "hpperlevel"),
        (GameStat.HealthRegen, "hpregen", "hpregenperlevel"),
        (GameStat.Mana, "mp", "mpperlevel"),
        (GameStat.ManaRegen, "mpregen", "mpregenperlevel"),
        (GameStat.AttackDamage, "attackdamage", "attackdamageperlevel"),
        (GameStat.AttackSpeed, "attackspeed", "attackspeedperlevel"),
        (GameStat.Armor, "armor", "armorperlevel"),
        (GameStat.MagicResist, "spellblock", "spellblockperlevel"),
        (GameStat.MoveSpeed, "movespeed", null),
        (GameStat.AttackRange, AttackRange.StatKey, null),
    ];
}
