using LoDb.Infrastructure.Robots.Detection;
using LoDb.Infrastructure.Robots.Identification;

namespace LoDb.Infrastructure.Robots.Reports.Reading;

/// <summary>
/// Every agent of the journal and its robot: the robot of a row is that of its agent when
/// its user agent named it, the undeclared robot of its network otherwise.
/// </summary>
internal sealed class AgentMap(IEnumerable<AgentEntry> agents)
{
    private readonly Dictionary<int, AgentEntry> _agents =
        agents.ToDictionary(static agent => agent.Id);

    public AgentEntry? Find(int id) => _agents.GetValueOrDefault(id);

    /// <param name="agentId">The row's agent.</param>
    /// <param name="detection">The row's detection.</param>
    /// <param name="network">The row's network; empty or null for none.</param>
    public RobotProfile ProfileOf(int agentId, string detection, string? network)
    {
        if (detection == RobotDetections.Rate)
        {
            return RobotCatalog.Undeclared(string.IsNullOrEmpty(network) ? null : network);
        }

        return _agents.TryGetValue(agentId, out var agent)
            ? agent.Declared
            : RobotCatalog.NoAgent;
    }

    /// <returns>The rows of the robot; null for a key no agent stands for.</returns>
    public RobotSelection? SelectionOf(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.StartsWith(RobotCatalog.UndeclaredPrefix, StringComparison.Ordinal))
        {
            var network = key[RobotCatalog.UndeclaredPrefix.Length..];
            return new RobotSelection
            {
                Detection = RobotDetections.Rate,
                Network = network == RobotCatalog.UnknownNetwork ? string.Empty : network,
            };
        }

        var ids = IdsWhere(agent => agent.Declared.Key == key);
        return ids.Count == 0
            ? null
            : new RobotSelection { Detection = RobotDetections.Agent, AgentIds = ids };
    }

    /// <summary>The rows of every robot of a category.</summary>
    public RobotSelection SelectionOfCategory(string category) =>
        category == RobotCategories.Undeclared
            ? new RobotSelection { Detection = RobotDetections.Rate }
            : new RobotSelection
            {
                Detection = RobotDetections.Agent,
                AgentIds = IdsWhere(agent => agent.Declared.Category == category),
            };

    private List<int> IdsWhere(Func<AgentEntry, bool> predicate) =>
        [.. _agents.Values.Where(predicate).Select(static agent => agent.Id)];
}
