using LoDb.Domain.Derived.Ranges;
using LoDb.Domain.Derived.Stats;

namespace LoDb.Api.Modules.Catalog.Champions;

/// <summary>
/// The Data Dragon keys of the champion stats the pages show, in their display order.
/// </summary>
internal static class ChampionStats
{
    private static readonly (GameStat Stat, string Base, string? PerLevel)[] Rows =
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

    /// <summary>The rows whose base value the version ships.</summary>
    public static IReadOnlyList<ChampionStat> Of(IReadOnlyDictionary<string, double> stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        return [.. Rows
            .Where(row => stats.ContainsKey(row.Base))
            .Select(row => new ChampionStat
            {
                Stat = row.Stat,
                Base = stats[row.Base],
                PerLevel = row.PerLevel is { } key && stats.TryGetValue(key, out var gain)
                    ? gain
                    : null,
            })];
    }
}
