using Npgsql;

namespace LoDb.Infrastructure.Audience;

/// <summary>
/// The SQL of <c>audience_entity_daily</c>: a day is replaced whole, in one transaction, so a
/// pull is idempotent and an entity Umami no longer counts that day leaves it.
/// </summary>
internal sealed class AudienceStore(NpgsqlDataSource dataSource)
{
    private const string DeleteSql = "DELETE FROM audience_entity_daily WHERE day = @day";

    private const string InsertSql = """
        INSERT INTO audience_entity_daily (day, entity, views, visitors)
        SELECT @day, pulled.entity, pulled.views, pulled.visitors
          FROM unnest(@entities, @views, @visitors) AS pulled(entity, views, visitors)
        """;

    /// <summary>Makes <paramref name="entities"/> the figures of <paramref name="day"/>.</summary>
    public async Task ReplaceDayAsync(
        DateOnly day,
        IReadOnlyCollection<EntityViews> entities,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entities);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var delete = new NpgsqlCommand(DeleteSql, connection, transaction))
        {
            delete.Parameters.AddWithValue("day", day);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insert = new NpgsqlCommand(InsertSql, connection, transaction))
        {
            insert.Parameters.AddWithValue("day", day);
            insert.Parameters.AddWithValue("entities", Column(entities, static row => row.Entity));
            insert.Parameters.AddWithValue("views", Column(entities, static row => row.Views));
            insert.Parameters.AddWithValue("visitors", Column(entities, static row => row.Visitors));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static T[] Column<T>(IEnumerable<EntityViews> rows, Func<EntityViews, T> value) =>
        [.. rows.Select(value)];
}
