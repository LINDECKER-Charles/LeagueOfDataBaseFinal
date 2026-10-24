namespace LoDb.Infrastructure.Analytics.Reports.Breakdowns;

/// <summary>A page of a breakdown: its dimension, a text its keys hold, the page.</summary>
public sealed record AnalyticsBreakdownQuery
{
    /// <summary>One of <see cref="AnalyticsDimensions.All"/>.</summary>
    public required string Dimension { get; init; }

    /// <summary>A text anywhere in the key, whatever its case; null for every key.</summary>
    public string? Search { get; init; }

    /// <summary>The page, from 1.</summary>
    public int Page { get; init; } = 1;
}
