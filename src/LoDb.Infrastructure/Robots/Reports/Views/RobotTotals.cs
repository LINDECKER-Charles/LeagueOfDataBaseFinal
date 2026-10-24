namespace LoDb.Infrastructure.Robots.Reports.Views;

/// <summary>The hits of every robot over a period.</summary>
public sealed record RobotTotals
{
    public required RobotFigures Figures { get; init; }

    /// <summary>Distinct robots seen.</summary>
    public required int Robots { get; init; }

    /// <summary>Hits of the robots that explore the site on their own.</summary>
    public required long CrawlerHits { get; init; }
}
