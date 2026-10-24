using LoDb.Infrastructure.Persistence.Roles;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace LoDb.Infrastructure.Robots.Retention;

/// <summary>
/// Deletes the hits past their retention, on the clock of <see cref="TimeProvider"/>. The
/// daily counts hold neither address nor path and are kept.
/// </summary>
/// <remarks>
/// The deletion reads the index on <c>occurred_at</c>; its cost grows with the robots'
/// traffic of the day it clears, so it gets <see cref="RobotsOptions.JobStatementTimeout"/>.
/// </remarks>
internal sealed class RobotRetention(
    NpgsqlDataSource dataSource,
    IOptions<RobotsOptions> options,
    TimeProvider timeProvider) : IRobotRetention
{
    private const string DeleteSql = "DELETE FROM robot_hit WHERE occurred_at < @cutoff";

    public async Task<int> ApplyAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var delete = new NpgsqlCommand(DeleteSql, connection);
        await StatementTimeouts.RaiseAsync(
            transaction,
            delete,
            settings.JobStatementTimeout,
            cancellationToken);
        delete.Parameters.Add(new NpgsqlParameter("cutoff", NpgsqlDbType.TimestampTz)
        {
            Value = (timeProvider.GetUtcNow() - settings.HitRetention).ToUniversalTime(),
        });
        var deleted = await delete.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return deleted;
    }
}
