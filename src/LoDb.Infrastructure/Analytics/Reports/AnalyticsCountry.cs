namespace LoDb.Infrastructure.Analytics.Reports;

/// <summary>A country of the audience, with its count and its share.</summary>
public sealed record AnalyticsCountry
{
    /// <summary>English name, or the code when none was known.</summary>
    public required string Name { get; init; }

    /// <summary>ISO 3166 alpha-2 code.</summary>
    public required string Code { get; init; }

    public required long Count { get; init; }

    /// <summary>Percentage of the views that have a country.</summary>
    public required double Pct { get; init; }
}
