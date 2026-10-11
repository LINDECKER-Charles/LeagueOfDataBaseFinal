using System.Text.Json;
using LoDb.Api.Modules.PublicApi.Trends.Reading;
using LoDb.Infrastructure.Audience;
using LoDb.Infrastructure.Persistence;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.PublicApi.Behaviour;

/// <summary>
/// The days <c>/v1/trends</c> ranks: the in-house aggregates alone until a switch date is set,
/// Umami's figures from it on, each entity's day bounded by the median of its recent days.
/// </summary>
public sealed class TrendsSourceTests(PostgresContainerFixture postgres)
{
    private const string EmptyObject = "{}";

    private static readonly DateOnly First = new(2026, 10, 1);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WithoutASwitchDateTheAggregatesAloneAreRead()
    {
        await using var database = await SeededAsync();
        await using var persistence = Persistence(database);

        var days = await Source(persistence, switchDate: null)
            .ReadEntitiesAsync(First, First.AddDays(3), Token);

        Assert.Equal(
            [Map(("champion:Ahri", 1)), Map(("champion:Ahri", 2)), Map(("champion:Ahri", 3))],
            Normalized(days));
    }

    [Fact]
    public async Task FromTheSwitchDateUmamiTakesOver()
    {
        await using var database = await SeededAsync();
        await using var persistence = Persistence(database);

        var days = await Source(persistence, switchDate: First.AddDays(2))
            .ReadEntitiesAsync(First, First.AddDays(3), Token);

        Assert.Equal(
            [
                Map(("champion:Ahri", 1)),
                Map(("champion:Ahri", 2)),
                Map(("champion:Ahri", 30), ("item:1036", 7)),
                Map(("champion:Ahri", 40)),
            ],
            Normalized(days));
    }

    [Fact]
    public async Task AWindowAfterTheSwitchReadsUmamiAlone()
    {
        await using var database = await SeededAsync();
        await using var persistence = Persistence(database);

        var days = await Source(persistence, switchDate: First)
            .ReadEntitiesAsync(First.AddDays(2), First.AddDays(2), Token);

        Assert.Equal([Map(("champion:Ahri", 30), ("item:1036", 7))], Normalized(days));
    }

    [Fact]
    public async Task ADayFarAboveItsMedianCountsThreeTimesIt()
    {
        await using var database = await postgres.CreateMigratedDatabaseAsync(Token);
        for (var day = 0; day < 10; day++)
        {
            await PullAsync(database, First.AddDays(day), ("champion:Ahri", 100));
        }

        await PullAsync(database, First.AddDays(10), ("champion:Ahri", 100_000));
        await PullAsync(database, First.AddDays(10), ("champion:Zed", 5_000));
        await PullAsync(database, First.AddDays(9), ("item:1036", 5));
        await PullAsync(database, First.AddDays(10), ("item:1036", 100_000));

        await using var persistence = Persistence(database);
        var days = await Source(persistence, switchDate: First)
            .ReadEntitiesAsync(First.AddDays(10), First.AddDays(10), Token);

        // Ahri: 3 x 100. Zed, on its first day, is its own median. The sword's two days give
        // the lower one as their median: 3 x 5.
        Assert.Equal(
            [Map(("champion:Ahri", 300), ("champion:Zed", 5_000), ("item:1036", 15))],
            Normalized(days));
    }

    // The persistence zone as the API registers it, on the database of the test.
    private static ServiceProvider Persistence(TestDatabase database)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:LoDb"] = database.ConnectionString,
            })
            .Build();
        return new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLoDbPersistence(configuration)
            .BuildServiceProvider();
    }

    private static TrendsSource Source(ServiceProvider persistence, DateOnly? switchDate) =>
        new(
            persistence.GetRequiredService<IDbContextFactory<LoDbDbContext>>(),
            Options.Create(new AudienceOptions { TrendsSwitchDate = switchDate }));

    // Three days of the in-house measure, then Umami's figures of the third and fourth days.
    private async Task<TestDatabase> SeededAsync()
    {
        var database = await postgres.CreateMigratedDatabaseAsync(Token);
        for (var day = 0; day < 3; day++)
        {
            var buckets = JsonSerializer.Serialize(new
            {
                entities = new Dictionary<string, int> { ["champion:Ahri"] = day + 1 },
            });
            await database.ExecuteAsync(
                $"""
                INSERT INTO analytics_daily
                    (day, source, totals, buckets, visitors, country_names, updated_at)
                VALUES ('{First.AddDays(day):yyyy-MM-dd}', 'import', '{EmptyObject}',
                    '{buckets}', '[]', '{EmptyObject}', now())
                """,
                Token);
        }

        await PullAsync(database, First.AddDays(2), ("champion:Ahri", 30));
        await PullAsync(database, First.AddDays(2), ("item:1036", 7));
        await PullAsync(database, First.AddDays(3), ("champion:Ahri", 40));
        return database;
    }

    // What a pull wrote for an entity on a day.
    private static Task PullAsync(
        TestDatabase database,
        DateOnly day,
        (string Entity, long Views) pulled) =>
        database.ExecuteAsync(
            $"""
            INSERT INTO audience_entity_daily (day, entity, views, visitors)
            VALUES ('{day:yyyy-MM-dd}', '{pulled.Entity}', {pulled.Views}, 1)
            """,
            Token);

    private static string Map(params (string Entity, long Views)[] entities) =>
        JsonSerializer.Serialize(entities.ToDictionary(
            static entity => entity.Entity,
            static entity => entity.Views));

    // The maps of the days in order, their keys sorted, as the ranking reads them.
    private static IEnumerable<string> Normalized(IReadOnlyList<string?> days) =>
        days.Select(static day => JsonSerializer.Serialize(
            JsonSerializer.Deserialize<Dictionary<string, long>>(day!)!
                .OrderBy(static entity => entity.Key, StringComparer.Ordinal)
                .ToDictionary()));
}
