using System.Globalization;
using LoDb.Infrastructure.Locks;
using Npgsql;

namespace LoDb.Infrastructure.Persistence.Analytics.Partitions;

internal sealed class AnalyticsPartitions(NpgsqlDataSource dataSource) : IAnalyticsPartitions
{
    private const string Lock = "SELECT pg_advisory_xact_lock(@key)";

    private const string List = $"""
        SELECT c.relname
        FROM pg_inherits i
        JOIN pg_class c ON c.oid = i.inhrelid
        WHERE i.inhparent = '{AnalyticsPartitionNames.Parent}'::regclass
        """;

    private static readonly long LockKey = PostgresDistributedLock.KeyOf("analytics:partitions");

    public async Task<IReadOnlyList<DateOnly>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await ListAsync(connection, null, cancellationToken);
    }

    public async Task<int> CreateAsync(
        DateOnly first,
        DateOnly last,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(first, last);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await LockAsync(connection, transaction, cancellationToken);
        var existing = await ListAsync(connection, transaction, cancellationToken);
        var missing = Days(first, last).Except(existing).ToList();
        foreach (var day in missing)
        {
            await ExecuteAsync(connection, transaction, CreateSql(day), cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return missing.Count;
    }

    public async Task<int> DropBeforeAsync(
        DateOnly firstKept,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await LockAsync(connection, transaction, cancellationToken);
        var expired = (await ListAsync(connection, transaction, cancellationToken))
            .Where(day => day < firstKept)
            .ToList();
        foreach (var day in expired)
        {
            var sql = $"DROP TABLE \"{AnalyticsPartitionNames.Of(day)}\"";
            await ExecuteAsync(connection, transaction, sql, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return expired.Count;
    }

    private static async Task LockAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(Lock, connection, transaction);
        command.Parameters.AddWithValue("key", LockKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<DateOnly>> ListAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(List, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var days = new List<DateOnly>();
        while (await reader.ReadAsync(cancellationToken))
        {
            if (AnalyticsPartitionNames.TryParseDay(reader.GetString(0), out var day))
            {
                days.Add(day.Value);
            }
        }

        days.Sort();
        return days;
    }

    private static IEnumerable<DateOnly> Days(DateOnly first, DateOnly last)
    {
        for (var day = first; day <= last; day = day.AddDays(1))
        {
            yield return day;
        }
    }

    // DDL takes no parameters: the name and the bounds come from a date, never from input.
    private static string CreateSql(DateOnly day) => string.Format(
        CultureInfo.InvariantCulture,
        "CREATE TABLE \"{0}\" PARTITION OF {1} FOR VALUES FROM ('{2:O}') TO ('{3:O}')",
        AnalyticsPartitionNames.Of(day),
        AnalyticsPartitionNames.Parent,
        AnalyticsPartitionNames.StartOf(day).UtcDateTime,
        AnalyticsPartitionNames.StartOf(day.AddDays(1)).UtcDateTime);

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
