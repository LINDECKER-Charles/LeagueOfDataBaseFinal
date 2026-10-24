namespace LoDb.Infrastructure.Robots.Reports.Detail;

/// <summary>A path a robot asked for, how often, and how it was last answered.</summary>
public sealed record RobotPathView
{
    /// <summary>The path and query, as requested.</summary>
    public required string Target { get; init; }

    public required long Hits { get; init; }

    public required short LastStatus { get; init; }

    public required DateTimeOffset LastAt { get; init; }
}
