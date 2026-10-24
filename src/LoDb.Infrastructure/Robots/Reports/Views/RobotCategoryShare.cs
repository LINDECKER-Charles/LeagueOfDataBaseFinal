namespace LoDb.Infrastructure.Robots.Reports.Views;

/// <summary>The hits of one category of robots over a period.</summary>
public sealed record RobotCategoryShare
{
    public required string Category { get; init; }

    public required long Hits { get; init; }

    /// <summary>Distinct robots of the category.</summary>
    public required int Robots { get; init; }

    public required double Pct { get; init; }
}
