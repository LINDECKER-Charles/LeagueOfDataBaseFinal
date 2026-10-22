using LoDb.Domain.Derived.Stats;

namespace LoDb.Api.Modules.Catalog.Champions;

/// <summary>
/// The champion stats the pages show, in their display order (<see cref="ChampionStatRows"/>).
/// </summary>
internal static class ChampionStats
{
    /// <summary>The rows whose base value the version ships.</summary>
    public static IReadOnlyList<ChampionStat> Of(IReadOnlyDictionary<string, double> stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        return [.. ChampionStatRows.All
            .Where(row => stats.ContainsKey(row.BaseKey))
            .Select(row => new ChampionStat
            {
                Stat = row.Stat,
                Base = stats[row.BaseKey],
                PerLevel = row.PerLevelKey is { } key && stats.TryGetValue(key, out var gain)
                    ? gain
                    : null,
            })];
    }
}
