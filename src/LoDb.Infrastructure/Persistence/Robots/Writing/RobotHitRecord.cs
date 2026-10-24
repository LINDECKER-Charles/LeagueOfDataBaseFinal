namespace LoDb.Infrastructure.Persistence.Robots.Writing;

/// <summary>
/// A hit as the robots journal hands it to <see cref="IRobotJournalWriter"/>: the row of
/// <c>robot_hit</c> with its agent's text in place of its id, every text already cut to its
/// column.
/// </summary>
public sealed record RobotHitRecord
{
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>The user agent as sent; empty without one.</summary>
    public required string Agent { get; init; }

    public required string Detection { get; init; }

    public required string Verdict { get; init; }

    public string? Network { get; init; }

    public required string Method { get; init; }

    public required string Target { get; init; }

    public required string Section { get; init; }

    public required short Status { get; init; }

    public required int DurationMs { get; init; }

    public required long Bytes { get; init; }

    public string? Cache { get; init; }
}
