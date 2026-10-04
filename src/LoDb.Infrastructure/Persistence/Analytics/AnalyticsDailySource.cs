namespace LoDb.Infrastructure.Persistence.Analytics;

/// <summary>
/// Where a daily aggregate comes from, stored as <c>events</c> or <c>import</c>.
/// </summary>
public enum AnalyticsDailySource
{
    /// <summary>Folded from <c>analytics_event</c>; folding the day again replaces it.</summary>
    Events,

    /// <summary>
    /// Taken from a legacy file, whose events were never kept: folding its day again would
    /// empty it, so a rollup leaves it alone.
    /// </summary>
    Import,
}
