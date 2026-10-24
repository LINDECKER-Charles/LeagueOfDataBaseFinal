namespace LoDb.Infrastructure.Robots.Reports.Journal;

/// <summary>One hit of the journal, its robot named.</summary>
public sealed record RobotHitView
{
    public required DateTimeOffset At { get; init; }

    public required string RobotKey { get; init; }

    public required string RobotName { get; init; }

    /// <summary>Who runs the robot; for an undeclared one, its network.</summary>
    public required string Operator { get; init; }

    public required string Category { get; init; }

    public required string Verdict { get; init; }

    public string? Network { get; init; }

    public required string Method { get; init; }

    public required string Target { get; init; }

    public required string Section { get; init; }

    public required short Status { get; init; }

    public required int DurationMs { get; init; }

    public required long Bytes { get; init; }

    /// <summary>nginx's cache status, such as <c>HIT</c>.</summary>
    public string? Cache { get; init; }
}
