namespace LoDb.Infrastructure.Analytics.Reports;

/// <summary>One day of the series of a report.</summary>
public sealed record AnalyticsDay
{
    public required DateOnly Date { get; init; }

    /// <summary>Views of people.</summary>
    public required long Views { get; init; }

    /// <summary>Distinct visitors of the day.</summary>
    public required int Visitors { get; init; }

    public required long BotViews { get; init; }
}
