using Npgsql;
using NpgsqlTypes;

namespace LoDb.Infrastructure.Persistence.Robots.Writing;

/// <summary>
/// The counts of a batch added to <c>robot_daily</c>: summed per day and dimensions here,
/// then added to the rows that hold them, created when missing.
/// </summary>
internal static class RobotDayUpsert
{
    private const string Sql = """
        INSERT INTO robot_daily AS kept (day, agent_id, detection, verdict, network, section,
            status, hits, bytes, duration_ms, slow_hits)
        SELECT * FROM unnest(@days, @agents, @detections, @verdicts, @networks, @sections,
            @statuses, @hits, @bytes, @durations, @slow)
        ORDER BY 1, 2, 3, 4, 5, 6, 7
        ON CONFLICT (day, agent_id, detection, verdict, network, section, status) DO UPDATE
            SET hits = kept.hits + excluded.hits,
                bytes = kept.bytes + excluded.bytes,
                duration_ms = kept.duration_ms + excluded.duration_ms,
                slow_hits = kept.slow_hits + excluded.slow_hits
        """;

    public static async Task RunAsync(
        NpgsqlTransaction transaction,
        IReadOnlyCollection<RobotHitRecord> hits,
        IReadOnlyDictionary<string, int> agents,
        CancellationToken cancellationToken)
    {
        var days = Sum(hits, agents);
        await using var command = new NpgsqlCommand(Sql, transaction.Connection, transaction);
        Add(command, "days", NpgsqlDbType.Date, days.Select(static day => day.Day));
        Add(command, "agents", NpgsqlDbType.Integer, days.Select(static day => day.AgentId));
        Add(command, "detections", NpgsqlDbType.Text, days.Select(static day => day.Detection));
        Add(command, "verdicts", NpgsqlDbType.Text, days.Select(static day => day.Verdict));
        Add(command, "networks", NpgsqlDbType.Text, days.Select(static day => day.Network));
        Add(command, "sections", NpgsqlDbType.Text, days.Select(static day => day.Section));
        Add(command, "statuses", NpgsqlDbType.Smallint, days.Select(static day => day.Status));
        Add(command, "hits", NpgsqlDbType.Bigint, days.Select(static day => day.Hits));
        Add(command, "bytes", NpgsqlDbType.Bigint, days.Select(static day => day.Bytes));
        Add(command, "durations", NpgsqlDbType.Bigint, days.Select(static day => day.DurationMs));
        Add(command, "slow", NpgsqlDbType.Bigint, days.Select(static day => day.SlowHits));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static List<RobotDay> Sum(
        IReadOnlyCollection<RobotHitRecord> hits,
        IReadOnlyDictionary<string, int> agents) =>
        [
            .. hits
                .GroupBy(hit => (
                    Day: DateOnly.FromDateTime(hit.OccurredAt.UtcDateTime),
                    AgentId: agents[hit.Agent],
                    hit.Detection,
                    hit.Verdict,
                    Network: hit.Network ?? string.Empty,
                    hit.Section,
                    hit.Status))
                .Select(static group => new RobotDay
                {
                    Day = group.Key.Day,
                    AgentId = group.Key.AgentId,
                    Detection = group.Key.Detection,
                    Verdict = group.Key.Verdict,
                    Network = group.Key.Network,
                    Section = group.Key.Section,
                    Status = group.Key.Status,
                    Hits = group.Count(),
                    Bytes = group.Sum(static hit => hit.Bytes),
                    DurationMs = group.Sum(static hit => (long)hit.DurationMs),
                    SlowHits = group.Count(static hit =>
                        hit.DurationMs >= RobotDay.SlowMilliseconds),
                }),
        ];

    private static void Add<T>(
        NpgsqlCommand command,
        string name,
        NpgsqlDbType type,
        IEnumerable<T> values) =>
        command.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Array | type)
        {
            Value = values.ToArray(),
        });
}
