using LoDb.Infrastructure.Persistence.Analytics;

namespace LoDb.Infrastructure.Analytics.Aggregation;

/// <summary>A row of <c>analytics_daily</c>, its JSON columns as text.</summary>
internal sealed record DailyRow
{
    public required DateOnly Day { get; init; }

    public required AnalyticsDailySource Source { get; init; }

    public required string Totals { get; init; }

    public required string Buckets { get; init; }

    public required string Visitors { get; init; }

    public required string CountryNames { get; init; }
}
