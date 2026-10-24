using LoDb.Infrastructure.Robots.Identification;

namespace LoDb.Infrastructure.Robots.Reports.Reading;

/// <summary>A row of <c>robot_agent</c> and the robot its text names today.</summary>
internal sealed record AgentEntry
{
    public required int Id { get; init; }

    public required string Agent { get; init; }

    public required DateTimeOffset FirstSeen { get; init; }

    public required DateTimeOffset LastSeen { get; init; }

    /// <summary>The robot its text names, <see cref="DeclaredOf"/>.</summary>
    public required RobotProfile Declared { get; init; }

    /// <summary>
    /// The robot the catalog names; an agent the catalog now takes for a browser's keeps a
    /// name of its own, as its hits were a robot's.
    /// </summary>
    public static RobotProfile DeclaredOf(string agent) =>
        RobotCatalog.Identify(agent) ?? UnidentifiedRobots.Of(agent);
}
