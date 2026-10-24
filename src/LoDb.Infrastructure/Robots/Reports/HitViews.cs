using LoDb.Infrastructure.Robots.Reports.Journal;
using LoDb.Infrastructure.Robots.Reports.Reading;

namespace LoDb.Infrastructure.Robots.Reports;

/// <summary>A row of the journal with its robot named.</summary>
internal static class HitViews
{
    public static RobotHitView Of(HitRow row, AgentMap map)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(map);
        var profile = map.ProfileOf(row.AgentId, row.Detection, row.Network);
        return new RobotHitView
        {
            At = row.At,
            RobotKey = profile.Key,
            RobotName = profile.Name,
            Operator = profile.Operator,
            Category = profile.Category,
            Verdict = row.Verdict,
            Network = row.Network,
            Method = row.Method,
            Target = row.Target,
            Section = row.Section,
            Status = row.Status,
            DurationMs = row.DurationMs,
            Bytes = row.Bytes,
            Cache = row.Cache,
        };
    }
}
