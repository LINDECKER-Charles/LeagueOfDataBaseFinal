namespace LoDb.Infrastructure.Persistence.Robots;

/// <summary>
/// A row of <c>robot_agent</c>: one user agent the robots journal met, as the client sent it,
/// and when it was first and last seen.
/// </summary>
/// <remarks>
/// The robot an agent stands for is never stored: the reports read it from the agent's text
/// through the catalog, so that a robot added to the catalog names its past hits too. An
/// empty text is a request without a user agent.
/// </remarks>
public sealed class RobotAgent
{
    public const int AgentMaxLength = 512;

    public int Id { get; set; }

    public required string Agent { get; set; }

    public DateTimeOffset FirstSeen { get; set; }

    public DateTimeOffset LastSeen { get; set; }
}
