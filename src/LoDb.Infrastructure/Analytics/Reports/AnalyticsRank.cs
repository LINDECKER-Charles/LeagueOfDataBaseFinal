namespace LoDb.Infrastructure.Analytics.Reports;

/// <summary>A key of a breakdown, with its count and its share of the breakdown's total.</summary>
public sealed record AnalyticsRank
{
    /// <summary>The key: a type, a path, <c>champion:Ahri</c>, a browser…</summary>
    public required string Name { get; init; }

    public required long Count { get; init; }

    /// <summary>Percentage of the whole breakdown, the keys past a top included.</summary>
    public required double Pct { get; init; }
}
