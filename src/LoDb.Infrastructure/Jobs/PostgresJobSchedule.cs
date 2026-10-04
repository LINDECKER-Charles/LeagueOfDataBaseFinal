using Npgsql;

namespace LoDb.Infrastructure.Jobs;

/// <summary>
/// <see cref="IJobSchedule"/> on the <c>periodic_job</c> table: one conditional upsert, so
/// concurrent starts are decided by the row lock of PostgreSQL.
/// </summary>
internal sealed class PostgresJobSchedule(NpgsqlDataSource dataSource) : IJobSchedule
{
    private const string Start = """
        INSERT INTO periodic_job (name, last_started_at)
        VALUES (@name, @now)
        ON CONFLICT (name) DO UPDATE SET last_started_at = excluded.last_started_at
        WHERE periodic_job.last_started_at <= @now - @interval
        """;

    private const string Success =
        "UPDATE periodic_job SET last_succeeded_at = @now WHERE name = @name";

    public async Task<bool> TryStartAsync(
        string job,
        DateTimeOffset now,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(Start);
        command.Parameters.AddWithValue("name", job);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("interval", interval);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task RecordSuccessAsync(
        string job,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(Success);
        command.Parameters.AddWithValue("name", job);
        command.Parameters.AddWithValue("now", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
