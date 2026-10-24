namespace LoDb.Infrastructure.Robots.Reports.Reading;

/// <summary>A row of <c>robot_hit</c> as the journal reads it.</summary>
internal sealed record HitRow
{
    public required DateTimeOffset At { get; init; }

    public required int AgentId { get; init; }

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
