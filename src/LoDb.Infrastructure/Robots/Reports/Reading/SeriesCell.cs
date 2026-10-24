namespace LoDb.Infrastructure.Robots.Reports.Reading;

/// <summary>
/// The hits of one day, one agent, detection and network, and one class of statuses.
/// </summary>
internal sealed record SeriesCell
{
    public required DateOnly Day { get; init; }

    public required int AgentId { get; init; }

    public required string Detection { get; init; }

    public required string Network { get; init; }

    /// <summary>The first digit of the statuses: 2 for <c>2xx</c>…</summary>
    public required int StatusClass { get; init; }

    public required long Hits { get; init; }
}
