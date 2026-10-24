namespace LoDb.Infrastructure.Robots.Reports.Reading;

/// <summary>
/// The counts of <c>robot_daily</c> over a period for one set of dimensions, the days summed.
/// </summary>
internal sealed record DailyCell
{
    public required int AgentId { get; init; }

    public required string Detection { get; init; }

    public required string Verdict { get; init; }

    /// <summary>Empty when the hits had none.</summary>
    public required string Network { get; init; }

    public required string Section { get; init; }

    public required short Status { get; init; }

    public required long Hits { get; init; }

    public required long Bytes { get; init; }

    public required long DurationMs { get; init; }

    public required long SlowHits { get; init; }

    public required DateOnly FirstDay { get; init; }

    public required DateOnly LastDay { get; init; }
}
