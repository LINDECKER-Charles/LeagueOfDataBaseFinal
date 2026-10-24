using LoDb.Infrastructure.Robots.Reports.Detail;
using LoDb.Infrastructure.Robots.Reports.Journal;
using Npgsql;
using NpgsqlTypes;

namespace LoDb.Infrastructure.Robots.Reports.Reading;

/// <summary>
/// The reads of <c>robot_hit</c>: the paths of a robot and the pages of the journal.
/// </summary>
internal sealed class RobotHitReader(NpgsqlDataSource dataSource)
{
    private const string Escape = "\\";

    private const string PathsSql = $"""
        SELECT target, count(*)::bigint, max(occurred_at),
            (array_agg(status ORDER BY occurred_at DESC))[1]
        FROM robot_hit
        WHERE occurred_at >= @since AND {SelectionFilter.Sql}
        GROUP BY target
        ORDER BY count(*) DESC, target
        LIMIT @limit
        """;

    private const string PageSql = $"""
        SELECT occurred_at, agent_id, detection, verdict, network, method, target, section,
            status, duration_ms, bytes, cache
        FROM robot_hit
        WHERE {SelectionFilter.Sql}
            AND (@status_class IS NULL OR status / 100 = @status_class)
            AND (@verdict IS NULL OR verdict = @verdict)
            AND (@section IS NULL OR section = @section)
            AND (@pattern IS NULL OR target ILIKE @pattern ESCAPE '{Escape}')
        ORDER BY occurred_at DESC, id DESC
        OFFSET @offset LIMIT @limit
        """;

    public async Task<IReadOnlyList<RobotPathView>> PathsAsync(
        RobotSelection selection,
        DateTimeOffset since,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(PathsSql);
        command.Parameters.Add(new NpgsqlParameter("since", NpgsqlDbType.TimestampTz)
        {
            Value = since.ToUniversalTime(),
        });
        command.Parameters.Add(Number("limit", limit));
        SelectionFilter.AddTo(command, selection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var paths = new List<RobotPathView>();
        while (await reader.ReadAsync(cancellationToken))
        {
            paths.Add(new RobotPathView
            {
                Target = reader.GetString(0),
                Hits = reader.GetInt64(1),
                LastAt = reader.GetFieldValue<DateTimeOffset>(2),
                LastStatus = reader.GetInt16(3),
            });
        }

        return paths;
    }

    /// <param name="selection">The robot or the category; null for every hit.</param>
    /// <param name="query">The other filters.</param>
    /// <param name="window">The rows skipped, then the rows read.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    public async Task<IReadOnlyList<HitRow>> PageAsync(
        RobotSelection? selection,
        RobotHitQuery query,
        (int Offset, int Limit) window,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var command = dataSource.CreateCommand(PageSql);
        SelectionFilter.AddTo(command, selection);
        AddFilters(command, query, window);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<HitRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(RowOf(reader));
        }

        return rows;
    }

    private static void AddFilters(
        NpgsqlCommand command,
        RobotHitQuery query,
        (int Offset, int Limit) window)
    {
        command.Parameters.Add(new NpgsqlParameter("status_class", NpgsqlDbType.Integer)
        {
            Value = query.StatusClass is { } statusClass ? statusClass : (object)DBNull.Value,
        });
        command.Parameters.Add(SelectionFilter.Text("verdict", query.Verdict));
        command.Parameters.Add(SelectionFilter.Text("section", query.Section));
        command.Parameters.Add(SelectionFilter.Text("pattern", query.TargetPattern));
        command.Parameters.Add(Number("offset", window.Offset));
        command.Parameters.Add(Number("limit", window.Limit));
    }

    private static NpgsqlParameter Number(string name, int value) =>
        new(name, NpgsqlDbType.Integer) { Value = value };

    private static HitRow RowOf(NpgsqlDataReader reader) => new()
    {
        At = reader.GetFieldValue<DateTimeOffset>(0),
        AgentId = reader.GetInt32(1),
        Detection = reader.GetString(2),
        Verdict = reader.GetString(3),
        Network = reader.IsDBNull(4) ? null : reader.GetString(4),
        Method = reader.GetString(5),
        Target = reader.GetString(6),
        Section = reader.GetString(7),
        Status = reader.GetInt16(8),
        DurationMs = reader.GetInt32(9),
        Bytes = reader.GetInt64(10),
        Cache = reader.IsDBNull(11) ? null : reader.GetString(11),
    };
}
