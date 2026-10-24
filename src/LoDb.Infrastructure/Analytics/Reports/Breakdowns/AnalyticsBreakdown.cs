namespace LoDb.Infrastructure.Analytics.Reports.Breakdowns;

/// <summary>A page of the keys of a breakdown over a period, the most viewed first.</summary>
public sealed record AnalyticsBreakdown
{
    public required string Range { get; init; }

    public required string Dimension { get; init; }

    /// <summary>The views of the whole dimension, every key counted.</summary>
    public required long Total { get; init; }

    /// <summary>The keys the search kept.</summary>
    public required int Matched { get; init; }

    /// <summary>The keys of the page; each share is of <see cref="Total"/>.</summary>
    public required IReadOnlyList<AnalyticsRank> Rows { get; init; }

    public required int Page { get; init; }

    /// <summary>Whether a next page follows.</summary>
    public required bool HasMore { get; init; }
}
