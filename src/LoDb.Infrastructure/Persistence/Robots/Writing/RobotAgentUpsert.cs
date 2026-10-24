using Npgsql;
using NpgsqlTypes;

namespace LoDb.Infrastructure.Persistence.Robots.Writing;

/// <summary>
/// The agents of a batch into <c>robot_agent</c>: a new one inserted, a known one's last
/// sight moved forward; every id returned, keyed by its text.
/// </summary>
internal static class RobotAgentUpsert
{
    // In the order of the text, so that two writers lock their rows in the same order.
    private const string Sql = """
        INSERT INTO robot_agent (agent, first_seen, last_seen)
        SELECT batch.agent, batch.seen, batch.seen
        FROM unnest(@agents, @seen) AS batch(agent, seen)
        ORDER BY batch.agent
        ON CONFLICT (agent) DO UPDATE
            SET last_seen = GREATEST(robot_agent.last_seen, excluded.last_seen)
        RETURNING id, agent
        """;

    public static async Task<IReadOnlyDictionary<string, int>> RunAsync(
        NpgsqlTransaction transaction,
        IReadOnlyCollection<RobotHitRecord> hits,
        CancellationToken cancellationToken)
    {
        var latest = hits
            .GroupBy(static hit => hit.Agent, StringComparer.Ordinal)
            .Select(static group =>
                (Agent: group.Key, Seen: group.Max(static hit => hit.OccurredAt)))
            .ToList();
        await using var command = new NpgsqlCommand(Sql, transaction.Connection, transaction);
        command.Parameters.Add(new NpgsqlParameter("agents", NpgsqlDbType.Array | NpgsqlDbType.Text)
        {
            Value = latest.Select(static entry => entry.Agent).ToArray(),
        });
        command.Parameters.Add(new NpgsqlParameter(
            "seen",
            NpgsqlDbType.Array | NpgsqlDbType.TimestampTz)
        {
            Value = latest.Select(static entry => entry.Seen.ToUniversalTime()).ToArray(),
        });
        var ids = new Dictionary<string, int>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids[reader.GetString(1)] = reader.GetInt32(0);
        }

        return ids;
    }
}
