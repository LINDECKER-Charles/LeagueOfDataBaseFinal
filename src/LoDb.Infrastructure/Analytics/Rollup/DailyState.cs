using LoDb.Infrastructure.Persistence.Analytics;

namespace LoDb.Infrastructure.Analytics.Rollup;

/// <summary>Who wrote a row of <c>analytics_daily</c>, and when it last did.</summary>
internal sealed record DailyState(AnalyticsDailySource Source, DateTimeOffset UpdatedAt);
