namespace LoDb.Domain.Derived.Stats;

/// <summary>
/// One non-zero row of an item stat block.
/// </summary>
public sealed record ItemStat
{
    public required GameStat Stat { get; init; }

    /// <summary>Whether <see cref="Value"/> is a fraction to read as a percentage.</summary>
    public required bool IsPercent { get; init; }

    public required double Value { get; init; }
}
