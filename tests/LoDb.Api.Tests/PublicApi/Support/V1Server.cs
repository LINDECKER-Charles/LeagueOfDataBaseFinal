using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using LoDb.Testing;
using Npgsql;
using NpgsqlTypes;

namespace LoDb.Api.Tests.PublicApi.Support;

/// <summary>
/// A PostgreSQL server holding go-api's <c>/v1</c> data set in a template database,
/// migrated and seeded once: every <see cref="V1Run"/> starts from a copy of its own.
/// </summary>
/// <remarks>
/// The data set dates the requests of the month from the day it is loaded; the daily
/// aggregates of the trends are dated from that day too, and the runs set their clock to it.
/// </remarks>
public sealed class V1Server : IAsyncLifetime
{
    private const string DayFormat = "yyyy-MM-dd";

    private readonly PostgresContainerFixture _postgres = new();
    private TestDatabase? _template;

    public static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The day the data set was loaded on, <c>CURRENT_DATE</c> of the server.</summary>
    public DateOnly SeedDay { get; private set; }

    private TestDatabase Template =>
        _template ?? throw new InvalidOperationException("The fixture is not initialized.");

    public async ValueTask InitializeAsync()
    {
        await _postgres.InitializeAsync();
        _template = await _postgres.CreateDatabaseAsync(Token);
        await _template.MigrateAsync(Token);
        var dataset = await File.ReadAllTextAsync(V1Fixtures.Seed("dataset.sql"), Token);
        await _template.ExecuteAsync(dataset, Token);
        var today = await _template.QueryAsync("SELECT CURRENT_DATE::text", Token);
        SeedDay = DateOnly.ParseExact(today[0], DayFormat, CultureInfo.InvariantCulture);
        await SeedDailyAggregatesAsync(_template, SeedDay);

        // Its pooled connections would keep the template from being copied.
        _template.DataSource.Clear();
    }

    /// <summary>The API over a fresh copy of the data set, extra settings applied last.</summary>
    public async Task<V1Run> StartAsync(IReadOnlyDictionary<string, string?>? settings = null)
    {
        var database = await V1Database.CopyAsync(_postgres.ConnectionString, Template.Name);
        return new V1Run(database, SeedDay, settings ?? new Dictionary<string, string?>());
    }

    public async ValueTask DisposeAsync()
    {
        if (_template is not null)
        {
            await _template.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }

    // daily.json dates each day by its offset from the seed day. Its corrupt file becomes an
    // entities map that is not one, and its day without entities a row without the map.
    private static async Task SeedDailyAggregatesAsync(TestDatabase database, DateOnly seedDay)
    {
        using var daily = JsonDocument.Parse(
            await File.ReadAllTextAsync(V1Fixtures.Seed("daily.json"), Token));
        foreach (var day in daily.RootElement.GetProperty("days").EnumerateArray())
        {
            var buckets = new JsonObject();
            if (day.TryGetProperty("entities", out var entities))
            {
                buckets["entities"] = JsonNode.Parse(entities.GetRawText());
            }
            else if (day.TryGetProperty("raw", out var raw))
            {
                buckets["entities"] = raw.GetString();
            }

            var date = seedDay.AddDays(-day.GetProperty("offset").GetInt32());
            await using var command = database.DataSource.CreateCommand(
                """
                INSERT INTO analytics_daily
                    (day, source, totals, buckets, visitors, country_names, updated_at)
                VALUES ($1, 'import', '{}', $2, '[]', '{}', now())
                """);
            command.Parameters.Add(new NpgsqlParameter { Value = date });
            command.Parameters.Add(new NpgsqlParameter
            {
                Value = buckets.ToJsonString(),
                NpgsqlDbType = NpgsqlDbType.Jsonb,
            });
            await command.ExecuteNonQueryAsync(Token);
        }
    }
}
