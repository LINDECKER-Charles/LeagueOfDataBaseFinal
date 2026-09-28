using LoDb.Infrastructure.Analytics.Aggregation;
using LoDb.Infrastructure.Persistence.Analytics;
using Npgsql;
using NpgsqlTypes;

namespace LoDb.Infrastructure.Analytics.Rollup;

/// <summary>
/// The SQL of <c>analytics_daily</c>. Never destructive: a rollup only replaces a row it
/// wrote itself (<c>events</c>), and an import only adds the days that have no row.
/// </summary>
internal sealed class DailyStore(NpgsqlDataSource dataSource, TimeProvider timeProvider)
{
    private const string Columns =
        "day, source, totals::text, buckets::text, visitors::text, country_names::text";

    private const string ReadSql = $"""
        SELECT {Columns} FROM analytics_daily
        WHERE day BETWEEN @first AND @last ORDER BY day
        """;

    private const string StatesSql = """
        SELECT day, source, updated_at FROM analytics_daily
        WHERE day BETWEEN @first AND @last
        """;

    private const string EarliestSql = "SELECT min(day) FROM analytics_daily";

    private const string ExistingSql = "SELECT day FROM analytics_daily WHERE day = ANY(@days)";

    private const string InsertSql = """
        INSERT INTO analytics_daily
            (day, source, totals, buckets, visitors, country_names, updated_at)
        VALUES (@day, @source, @totals, @buckets, @visitors, @country_names, @now)
        ON CONFLICT (day)
        """;

    private const string UpsertSql = InsertSql + """

        DO UPDATE SET totals = EXCLUDED.totals, buckets = EXCLUDED.buckets,
            visitors = EXCLUDED.visitors, country_names = EXCLUDED.country_names,
            updated_at = EXCLUDED.updated_at
        WHERE analytics_daily.source = @source
        """;

    private const string AddSql = InsertSql + " DO NOTHING";

    public async Task<IReadOnlyList<DailyRow>> ReadAsync(
        DateOnly first,
        DateOnly last,
        CancellationToken cancellationToken)
    {
        await using var command = Between(ReadSql, first, last);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<DailyRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new DailyRow
            {
                Day = reader.GetFieldValue<DateOnly>(0),
                Source = AnalyticsColumns.ParseSource(reader.GetString(1)),
                Totals = reader.GetString(2),
                Buckets = reader.GetString(3),
                Visitors = reader.GetString(4),
                CountryNames = reader.GetString(5),
            });
        }

        return rows;
    }

    /// <summary>Source and last write of the rows between two days.</summary>
    public async Task<IReadOnlyDictionary<DateOnly, DailyState>> StatesAsync(
        DateOnly first,
        DateOnly last,
        CancellationToken cancellationToken)
    {
        await using var command = Between(StatesSql, first, last);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var states = new Dictionary<DateOnly, DailyState>();
        while (await reader.ReadAsync(cancellationToken))
        {
            states[reader.GetFieldValue<DateOnly>(0)] = new DailyState(
                AnalyticsColumns.ParseSource(reader.GetString(1)),
                reader.GetFieldValue<DateTimeOffset>(2));
        }

        return states;
    }

    public async Task<DateOnly?> EarliestAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(EarliestSql);
        var earliest = await command.ExecuteScalarAsync(cancellationToken);
        return earliest is DateOnly day ? day : null;
    }

    public async Task<IReadOnlySet<DateOnly>> ExistingAsync(
        IReadOnlyCollection<DateOnly> days,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(ExistingSql);
        command.Parameters.AddWithValue("days", days.ToArray());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var existing = new HashSet<DateOnly>();
        while (await reader.ReadAsync(cancellationToken))
        {
            existing.Add(reader.GetFieldValue<DateOnly>(0));
        }

        return existing;
    }

    /// <summary>Writes the day as folded from its events, unless it was imported.</summary>
    /// <returns>Whether the row was written.</returns>
    public Task<bool> UpsertEventsAsync(
        DailyAggregate daily,
        CancellationToken cancellationToken) =>
        WriteAsync(UpsertSql, (daily, AnalyticsDailySource.Events), cancellationToken);

    /// <summary>Adds an imported day, unless the day has a row.</summary>
    /// <returns>Whether the row was added.</returns>
    public Task<bool> AddImportedAsync(
        DailyAggregate daily,
        CancellationToken cancellationToken) =>
        WriteAsync(AddSql, (daily, AnalyticsDailySource.Import), cancellationToken);

    private async Task<bool> WriteAsync(
        string sql,
        (DailyAggregate Daily, AnalyticsDailySource Source) row,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("day", row.Daily.Day);
        command.Parameters.AddWithValue("source", AnalyticsColumns.ToText(row.Source));
        AddJson(command, "totals", DailyColumns.TotalsOf(row.Daily));
        AddJson(command, "buckets", DailyColumns.BucketsOf(row.Daily));
        AddJson(command, "visitors", DailyColumns.VisitorsOf(row.Daily));
        AddJson(command, "country_names", DailyColumns.CountryNamesOf(row.Daily));
        command.Parameters.AddWithValue("now", timeProvider.GetUtcNow());
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private NpgsqlCommand Between(string sql, DateOnly first, DateOnly last)
    {
        var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("first", first);
        command.Parameters.AddWithValue("last", last);
        return command;
    }

    private static void AddJson(NpgsqlCommand command, string name, string json) =>
        command.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Jsonb) { Value = json });
}
