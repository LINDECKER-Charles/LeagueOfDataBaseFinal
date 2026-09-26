namespace LoDb.Infrastructure.Analytics.Reports;

/// <summary>The totals of a report's period.</summary>
public sealed record AnalyticsTotals
{
    /// <summary>Views of people; robots are only counted in <see cref="BotViews"/>.</summary>
    public required long Views { get; init; }

    public required long BotViews { get; init; }

    /// <summary>Distinct visitors of the period: the union of the days', not their sum.</summary>
    public required int UniqueVisitors { get; init; }

    /// <summary>Visitors seen on two days of the period or more.</summary>
    public required int ReturningVisitors { get; init; }

    /// <summary>Distinct pages viewed.</summary>
    public required int PagesTracked { get; init; }
}
