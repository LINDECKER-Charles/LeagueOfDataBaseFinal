using LoDb.Infrastructure.Analytics.Aggregation;
using LoDb.Infrastructure.Persistence.Analytics;
using LoDb.Infrastructure.Persistence.Analytics.Partitions;
using Npgsql;

namespace LoDb.Infrastructure.Analytics.Rollup;

/// <summary>
/// Folds the views of one UTC day, read from its partition alone, in the order they came.
/// </summary>
/// <remarks>Neither the address nor the user agent is read: no aggregate holds them.</remarks>
internal sealed class DayEvents(NpgsqlDataSource dataSource)
{
    private const string Sql = """
        SELECT occurred_at, route, path, type, kind, entity, status, lang, locale, visitor,
            browser, os, device, is_bot, referer_host, referer_source, country, country_name
        FROM analytics_event
        WHERE occurred_at >= @from AND occurred_at < @to
        ORDER BY occurred_at, id
        """;

    public async Task<DailyAggregate> FoldAsync(DateOnly day, CancellationToken cancellationToken)
    {
        var daily = new DailyAggregate(day);
        await using var command = dataSource.CreateCommand(Sql);
        command.Parameters.AddWithValue("from", AnalyticsPartitionNames.StartOf(day));
        command.Parameters.AddWithValue("to", AnalyticsPartitionNames.StartOf(day.AddDays(1)));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            DailyFold.Fold(daily, Read(reader));
        }

        return daily;
    }

    private static AnalyticsEvent Read(NpgsqlDataReader reader) => new()
    {
        OccurredAt = reader.GetFieldValue<DateTimeOffset>(0),
        Route = reader.GetString(1),
        Path = reader.GetString(2),
        Type = reader.GetString(3),
        Kind = reader.GetString(4),
        Entity = NullableText(reader, 5),
        Status = reader.GetInt16(6),
        Lang = NullableText(reader, 7),
        Locale = reader.GetString(8),
        Visitor = reader.GetString(9),
        Browser = reader.GetString(10),
        Os = reader.GetString(11),
        Device = reader.GetString(12),
        IsBot = reader.GetBoolean(13),
        RefererHost = NullableText(reader, 14),
        RefererSource = reader.GetString(15),
        Country = NullableText(reader, 16),
        CountryName = NullableText(reader, 17),
    };

    private static string? NullableText(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
}
