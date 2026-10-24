using Npgsql;
using NpgsqlTypes;

namespace LoDb.Infrastructure.Persistence.Robots.Writing;

/// <summary>
/// <see cref="IRobotJournalWriter"/> on Postgres: an upsert of the agents, a binary copy of
/// the hits, an upsert of the day counts, all in one transaction.
/// </summary>
internal sealed class RobotJournalWriter(NpgsqlDataSource dataSource) : IRobotJournalWriter
{
    private const string Copy = """
        COPY robot_hit (occurred_at, agent_id, status, duration_ms, bytes, detection, verdict,
            network, method, target, section, cache)
        FROM STDIN (FORMAT BINARY)
        """;

    private static readonly Func<RobotHitRecord, string?>[] TextColumns =
    [
        static hit => hit.Detection,
        static hit => hit.Verdict,
        static hit => hit.Network,
        static hit => hit.Method,
        static hit => hit.Target,
        static hit => hit.Section,
        static hit => hit.Cache,
    ];

    public async Task WriteAsync(
        IReadOnlyCollection<RobotHitRecord> hits,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hits);
        if (hits.Count == 0)
        {
            return;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var agents = await RobotAgentUpsert.RunAsync(transaction, hits, cancellationToken);
        await CopyAsync(connection, hits, agents, cancellationToken);
        await RobotDayUpsert.RunAsync(transaction, hits, agents, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task CopyAsync(
        NpgsqlConnection connection,
        IReadOnlyCollection<RobotHitRecord> hits,
        IReadOnlyDictionary<string, int> agents,
        CancellationToken cancellationToken)
    {
        await using var importer = await connection.BeginBinaryImportAsync(Copy, cancellationToken);
        foreach (var hit in hits)
        {
            await importer.StartRowAsync(cancellationToken);
            await importer.WriteAsync(
                hit.OccurredAt.ToUniversalTime(),
                NpgsqlDbType.TimestampTz,
                cancellationToken);
            await importer.WriteAsync(agents[hit.Agent], NpgsqlDbType.Integer, cancellationToken);
            await importer.WriteAsync(hit.Status, NpgsqlDbType.Smallint, cancellationToken);
            await importer.WriteAsync(hit.DurationMs, NpgsqlDbType.Integer, cancellationToken);
            await importer.WriteAsync(hit.Bytes, NpgsqlDbType.Bigint, cancellationToken);
            foreach (var column in TextColumns)
            {
                var text = column(hit);
                await (text is null
                    ? importer.WriteNullAsync(cancellationToken)
                    : importer.WriteAsync(text, NpgsqlDbType.Varchar, cancellationToken));
            }
        }

        await importer.CompleteAsync(cancellationToken);
    }
}
