namespace LoDb.Infrastructure.Robots.Reports.Reading;

/// <summary>
/// The rows of a robot or a category, as SQL filters them: a detection, its agents and, for
/// an undeclared robot, its network (empty for none).
/// </summary>
internal sealed record RobotSelection
{
    public required string Detection { get; init; }

    /// <summary>The agents; null for every agent.</summary>
    public IReadOnlyList<int>? AgentIds { get; init; }

    /// <summary>The network; null for every network, empty for the hits without one.</summary>
    public string? Network { get; init; }
}
