using LoDb.Domain.Derived.Stats;

namespace LoDb.Api.Modules.Catalog.Champions;

/// <summary>A base stat of a champion at level 1, and what it gains per level.</summary>
internal sealed record ChampionStat
{
    public required GameStat Stat { get; init; }

    public required double Base { get; init; }

    /// <summary>
    /// Gain per level, as Data Dragon writes it (a percentage for the attack speed); null for
    /// the stats that do not grow.
    /// </summary>
    public double? PerLevel { get; init; }
}
