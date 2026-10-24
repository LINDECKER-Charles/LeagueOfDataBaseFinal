using Npgsql;
using NpgsqlTypes;

namespace LoDb.Infrastructure.Robots.Reports.Reading;

/// <summary>The reads of <c>robot_agent</c> and <c>robot_daily</c> the reports need.</summary>
internal sealed class RobotDailyReader(NpgsqlDataSource dataSource)
{
    private const string AgentsSql =
        "SELECT id, agent, first_seen, last_seen FROM robot_agent";

    private const string EarliestSql = "SELECT min(day) FROM robot_daily";

    private const string TotalSql = """
        SELECT coalesce(sum(hits), 0)::bigint FROM robot_daily WHERE day >= @from AND day <= @to
        """;

    private const string CellsSql = $"""
        SELECT agent_id, detection, verdict, network, section, status,
            sum(hits)::bigint, sum(bytes)::bigint, sum(duration_ms)::bigint,
            sum(slow_hits)::bigint, min(day), max(day)
        FROM robot_daily
        WHERE day >= @from AND day <= @to AND {SelectionFilter.Sql}
        GROUP BY agent_id, detection, verdict, network, section, status
        """;

    private const string SeriesSql = $"""
        SELECT day, agent_id, detection, network, status / 100, sum(hits)::bigint
        FROM robot_daily
        WHERE day >= @from AND day <= @to AND {SelectionFilter.Sql}
        GROUP BY day, agent_id, detection, network, status / 100
        """;

    public async Task<AgentMap> AgentsAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(AgentsSql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var agents = new List<AgentEntry>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var agent = reader.GetString(1);
            agents.Add(new AgentEntry
            {
                Id = reader.GetInt32(0),
                Agent = agent,
                FirstSeen = reader.GetFieldValue<DateTimeOffset>(2),
                LastSeen = reader.GetFieldValue<DateTimeOffset>(3),
                Declared = AgentEntry.DeclaredOf(agent),
            });
        }

        return new AgentMap(agents);
    }

    /// <returns>The first day counted; null before any hit.</returns>
    public async Task<DateOnly?> EarliestAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(EarliestSql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) && !reader.IsDBNull(0)
            ? reader.GetFieldValue<DateOnly>(0)
            : null;
    }

    /// <summary>The hits of every robot over the period, the share of one robot's base.</summary>
    public async Task<long> TotalAsync(DatePeriod period, CancellationToken cancellationToken)
    {
        await using var command = Command(TotalSql, period, selection: null);
        return (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
    }

    public async Task<IReadOnlyList<DailyCell>> CellsAsync(
        DatePeriod period,
        RobotSelection? selection,
        CancellationToken cancellationToken)
    {
        await using var command = Command(CellsSql, period, selection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var cells = new List<DailyCell>();
        while (await reader.ReadAsync(cancellationToken))
        {
            cells.Add(new DailyCell
            {
                AgentId = reader.GetInt32(0),
                Detection = reader.GetString(1),
                Verdict = reader.GetString(2),
                Network = reader.GetString(3),
                Section = reader.GetString(4),
                Status = reader.GetInt16(5),
                Hits = reader.GetInt64(6),
                Bytes = reader.GetInt64(7),
                DurationMs = reader.GetInt64(8),
                SlowHits = reader.GetInt64(9),
                FirstDay = reader.GetFieldValue<DateOnly>(10),
                LastDay = reader.GetFieldValue<DateOnly>(11),
            });
        }

        return cells;
    }

    public async Task<IReadOnlyList<SeriesCell>> SeriesAsync(
        DatePeriod period,
        RobotSelection? selection,
        CancellationToken cancellationToken)
    {
        await using var command = Command(SeriesSql, period, selection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var cells = new List<SeriesCell>();
        while (await reader.ReadAsync(cancellationToken))
        {
            cells.Add(new SeriesCell
            {
                Day = reader.GetFieldValue<DateOnly>(0),
                AgentId = reader.GetInt32(1),
                Detection = reader.GetString(2),
                Network = reader.GetString(3),
                StatusClass = reader.GetInt32(4),
                Hits = reader.GetInt64(5),
            });
        }

        return cells;
    }

    private NpgsqlCommand Command(string sql, DatePeriod period, RobotSelection? selection)
    {
        var command = dataSource.CreateCommand(sql);
        command.Parameters.Add(Day("from", period.From));
        command.Parameters.Add(Day("to", period.To));
        SelectionFilter.AddTo(command, selection);
        return command;
    }

    private static NpgsqlParameter Day(string name, DateOnly day) =>
        new(name, NpgsqlDbType.Date) { Value = day };
}
