namespace LoDb.Infrastructure.Persistence.Analytics;

/// <summary>
/// A row of <c>analytics_daily</c>: the aggregate of one UTC day, in the shape of the legacy
/// files <c>analytics/daily/{date}.json</c>, split in four JSON columns.
/// </summary>
/// <remarks>
/// A legacy file maps onto a row without conversion: <see cref="Totals"/> takes
/// <c>views</c>, <c>botViews</c>, <c>byHour</c> and <c>byWeekday</c>;
/// <see cref="Buckets"/> the fifteen counter maps (<c>byType</c>, <c>byKind</c>,
/// <c>byRoute</c>, <c>status</c>, <c>pages</c>, <c>entities</c>, <c>heatmap</c>,
/// <c>locale</c>, <c>lang</c>, <c>browser</c>, <c>os</c>, <c>device</c>, <c>refSource</c>,
/// <c>refHost</c>, <c>country</c>); <see cref="Visitors"/> the list of visitor hashes;
/// <see cref="CountryNames"/> the labels of the countries. <c>/v1/trends</c> sums
/// <c>buckets -&gt; 'entities'</c>, keyed <c>{type}:{key}</c>.
/// </remarks>
public sealed class AnalyticsDaily
{
    public DateOnly Day { get; set; }

    public AnalyticsDailySource Source { get; set; }

    /// <summary>
    /// A JSON object: <c>views</c>, <c>botViews</c>, <c>byHour</c>, <c>byWeekday</c>.
    /// </summary>
    public required string Totals { get; set; }

    /// <summary>A JSON object of counter maps, each <c>{key: count}</c>.</summary>
    public required string Buckets { get; set; }

    /// <summary>A JSON array of the distinct visitor hashes of the day.</summary>
    public required string Visitors { get; set; }

    /// <summary>A JSON object <c>{code: name}</c> of the countries seen.</summary>
    public required string CountryNames { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
