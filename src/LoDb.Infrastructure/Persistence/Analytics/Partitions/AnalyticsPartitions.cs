using LoDb.Infrastructure.Locks;
using Npgsql;

namespace LoDb.Infrastructure.Persistence.Analytics.Partitions;

/// <remarks>
/// The API's role owns no table: a partition is created and dropped by the two functions
/// <c>migrate</c> installs (<c>Persistence/Roles/schema-privileges.sql</c>), which run as the
/// owner of <c>analytics_event</c> and derive the name and the bounds from the day alone.
/// </remarks>
internal sealed class AnalyticsPartitions(NpgsqlDataSource dataSource) : IAnalyticsPartitions
{
    private const string Lock = "SELECT pg_advisory_xact_lock(@key)";
    private const string Create = "SELECT public.lodb_create_analytics_partition(@day)";
    private const string Drop = "SELECT public.lodb_drop_analytics_partition(@day)";
    private const string DayParameter = "day";

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
            await ExecuteAsync(transaction, Create, day, cancellationToken);
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
            await ExecuteAsync(transaction, Drop, day, cancellationToken);
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

    private static async Task ExecuteAsync(
        NpgsqlTransaction transaction,
        string sql,
        DateOnly day,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, transaction.Connection, transaction);
        command.Parameters.AddWithValue(DayParameter, day);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
