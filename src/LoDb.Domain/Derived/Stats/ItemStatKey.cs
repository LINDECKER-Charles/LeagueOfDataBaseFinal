namespace LoDb.Domain.Derived.Stats;

/// <summary>
/// A Data Dragon item stat key and the stat it measures.
/// </summary>
public sealed record ItemStatKey
{
    /// <summary>Key in the item's <c>stats</c> block ("FlatPhysicalDamageMod").</summary>
    public required string DdragonKey { get; init; }

    public required GameStat Stat { get; init; }

    /// <summary>
    /// Whether the value is a fraction to read as a percentage (0.25 is 25 %).
    /// </summary>
    public required bool IsPercent { get; init; }
}
