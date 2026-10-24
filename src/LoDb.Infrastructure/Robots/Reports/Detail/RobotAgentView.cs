namespace LoDb.Infrastructure.Robots.Reports.Detail;

/// <summary>A user agent a robot sent, and its hits over the period.</summary>
public sealed record RobotAgentView
{
    /// <summary>As sent; empty for a request without one.</summary>
    public required string Agent { get; init; }

    public required long Hits { get; init; }

    public required DateTimeOffset FirstSeen { get; init; }

    public required DateTimeOffset LastSeen { get; init; }
}
