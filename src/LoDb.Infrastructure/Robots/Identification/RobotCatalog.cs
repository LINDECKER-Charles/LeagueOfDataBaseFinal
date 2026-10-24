using LoDb.Infrastructure.Robots.Identification.Tables;

namespace LoDb.Infrastructure.Robots.Identification;

/// <summary>
/// Tells which robot a user agent belongs to: the known robots in order, then the marks of
/// an unknown one; a browser's user agent names none.
/// </summary>
/// <remarks>
/// A fetcher or a preview comes before the crawler of the same operator, a specific token
/// before a general one (<c>googlebot-image</c> before <c>googlebot</c>). An unknown robot
/// that names itself comes before the library it is built on, which comes before a mere mark
/// (a contact link): <c>ExampleBot python-requests/2</c> is ExampleBot. The reports read the
/// robot of each agent here when they run, so that a change of the catalog renames the past
/// hits too.
/// </remarks>
internal static class RobotCatalog
{
    public const string UndeclaredPrefix = "undeclared:";

    /// <summary>The network part of an undeclared robot's key when none was found.</summary>
    public const string UnknownNetwork = "unknown";

    private const string NoAgentKey = "no-agent";
    private const string NoAgentName = "No user agent";
    private const string UndeclaredName = "Undeclared browser";

    /// <summary>The robots the catalog names, the libraries and scanners aside.</summary>
    public static IReadOnlyList<RobotProfile> Known { get; } =
    [
        .. AssistantRobots.All,
        .. AiRobots.All,
        .. SocialRobots.All,
        .. SeoRobots.All,
        .. MonitoringRobots.All,
        .. ArchiveRobots.All,
        .. SearchRobots.All,
    ];

    /// <summary>A request that sent no user agent at all.</summary>
    public static RobotProfile NoAgent { get; } = new()
    {
        Key = NoAgentKey,
        Name = NoAgentName,
        Operator = string.Empty,
        Category = RobotCategories.Tool,
    };

    /// <returns>The robot the agent names; null for a browser's.</returns>
    public static RobotProfile? Identify(string? userAgent)
    {
        var agent = userAgent?.Trim() ?? string.Empty;
        if (agent.Length == 0)
        {
            return NoAgent;
        }

        var lower = agent.ToLowerInvariant();
        return Known.FirstOrDefault(profile => profile.Matches(lower))
            ?? UnidentifiedRobots.Named(agent)
            ?? ToolAgents.All.FirstOrDefault(profile => profile.Matches(lower))
            ?? UnidentifiedRobots.Marked(agent, lower);
    }

    /// <summary>
    /// The robot behind a browser's user agent that its pace gave away, one per network: a
    /// sweep from a cloud reads as such.
    /// </summary>
    public static RobotProfile Undeclared(string? network) => new()
    {
        Key = UndeclaredPrefix + (string.IsNullOrEmpty(network) ? UnknownNetwork : network),
        Name = UndeclaredName,
        Operator = network ?? string.Empty,
        Category = RobotCategories.Undeclared,
    };
}
