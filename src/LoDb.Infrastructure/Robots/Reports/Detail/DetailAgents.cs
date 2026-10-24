using LoDb.Infrastructure.Robots.Reports.Reading;

namespace LoDb.Infrastructure.Robots.Reports.Detail;

/// <summary>The user agents of a robot's cells, the most frequent first.</summary>
internal static class DetailAgents
{
    public static IReadOnlyList<RobotAgentView> Of(IReadOnlyList<DailyCell> cells, AgentMap map)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(map);
        return
        [
            .. cells.GroupBy(static cell => cell.AgentId)
                .Select(group => (
                    Agent: map.Find(group.Key),
                    Hits: group.Sum(static cell => cell.Hits)))
                .Where(static entry => entry.Agent is not null)
                .Select(static entry => new RobotAgentView
                {
                    Agent = entry.Agent!.Agent,
                    Hits = entry.Hits,
                    FirstSeen = entry.Agent.FirstSeen,
                    LastSeen = entry.Agent.LastSeen,
                })
                .OrderByDescending(static view => view.Hits)
                .ThenBy(static view => view.Agent, StringComparer.Ordinal),
        ];
    }
}
