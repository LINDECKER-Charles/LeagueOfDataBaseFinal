using System.Globalization;
using System.Text.Json.Nodes;
using LoDb.Api.Modules.PublicApi;
using LoDb.Api.Modules.PublicApi.Metering;
using LoDb.Api.Modules.PublicApi.Trends.Reading;
using LoDb.Infrastructure.Persistence.PublicApi;
using LoDb.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Api.Tests.PublicApi.Support;

/// <summary>
/// The API over a copy of the data set of its own, as go-api was captured: its clock stopped
/// at noon of the seed day, its trends named from the seed storage.
/// </summary>
/// <remarks>
/// Time only moves when a test moves it, so the flusher of the metering never runs on its
/// own: <see cref="SleepAsync"/> moves the clock, then flushes as the flusher would have.
/// </remarks>
public sealed class V1Run : IAsyncDisposable
{
    public const string ApiKeyHeader = "X-Api-Key";

    private static readonly TimeSpan Noon = TimeSpan.FromHours(12);

    private readonly ApiFactory _factory;
    private readonly WebApplicationFactory<Program> _host;
    private string? _hiddenStorage;

    internal V1Run(
        V1Database database,
        DateOnly seedDay,
        IReadOnlyDictionary<string, string?> settings)
    {
        Database = database;
        SeedDay = seedDay;
        Clock = new FakeTimeProvider(
            new DateTimeOffset(seedDay.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) + Noon);
        _factory = new ApiFactory
        {
            PostgresConnectionString = database.ConnectionString,
            Clock = Clock,
            Settings = settings,
        };
        _host = _factory.WithWebHostBuilder(static builder => builder.ConfigureTestServices(
            static services => services.AddSingleton<ITrendNames>(SeedTrendNames.Instance)));
        Client = _host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    public V1Database Database { get; }

    /// <summary>The day the data set was loaded on, its usage dated from it.</summary>
    public DateOnly SeedDay { get; }

    /// <summary>The clock of the API, at noon of <see cref="SeedDay"/> until moved.</summary>
    public FakeTimeProvider Clock { get; }

    public HttpClient Client { get; }

    public IServiceProvider Services => _host.Services;

    /// <summary>A <c>GET</c>, with the key in <c>X-Api-Key</c> when there is one.</summary>
    public async Task<HttpResponseMessage> GetAsync(string path, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        if (key is not null)
        {
            request.Headers.TryAddWithoutValidation(ApiKeyHeader, key);
        }

        return await Client.SendAsync(request, V1Server.Token);
    }

    /// <summary>A <c>GET</c> read whole, the key in <c>X-Api-Key</c> when there is one.</summary>
    public async Task<V1Answer> AskAsync(string path, string? key = null)
    {
        using var response = await GetAsync(path, key);
        return await V1Answer.ReadAsync(response);
    }

    /// <summary>Adds the requests counted so far to <c>api_usage</c>.</summary>
    public Task FlushUsageAsync() =>
        Services.GetRequiredService<UsageMeter>().FlushAsync(V1Server.Token);

    /// <summary>Lets <paramref name="time"/> pass, and the flushes it leaves time for.</summary>
    public async Task SleepAsync(TimeSpan time)
    {
        Clock.Advance(time);
        await FlushUsageAsync();
    }

    /// <summary>
    /// Changes <c>api_keys</c> as the portal, the Stripe webhooks or the admin do: the
    /// statement, then <see cref="IApiKeyCache.Invalidate"/> with each key it changed.
    /// </summary>
    public async Task ChangeKeysAsync(string sql)
    {
        var before = await KeyRowsAsync();
        await Database.ExecuteAsync(sql);
        var after = await KeyRowsAsync();
        var changed = after
            .Where(row => !before.TryGetPropertyValue(row.Key, out var old)
                || !JsonNode.DeepEquals(old, row.Value))
            .Select(static row => int.Parse(row.Key, CultureInfo.InvariantCulture))
            .ToList();
        Assert.NotEmpty(changed);

        await using var context = Database.CreateContext();
        var keys = await context.ApiKeys.AsNoTracking()
            .Where(key => changed.Contains(key.Id))
            .ToListAsync(V1Server.Token);
        foreach (var key in keys)
        {
            Invalidate(key);
        }
    }

    /// <summary>The row of <c>api_keys</c>, as the portal or the admin hold it.</summary>
    public async Task<ApiKey> KeyAsync(int id)
    {
        await using var context = Database.CreateContext();
        return await context.ApiKeys.AsNoTracking()
            .SingleAsync(key => key.Id == id, V1Server.Token);
    }

    /// <summary>What the portal, the Stripe webhooks and the admin call after a change.</summary>
    public void Invalidate(ApiKey key) =>
        Services.GetRequiredService<IApiKeyCache>().Invalidate(key);

    /// <summary>Takes the storage away from under the API, until the run ends.</summary>
    public void HideStorage()
    {
        _hiddenStorage = _factory.StorageRoot + ".hidden";
        Directory.Move(_factory.StorageRoot, _hiddenStorage);
    }

    /// <summary>Stops the API as a shutdown does, with the last flush of the metering.</summary>
    public async Task StopApiAsync()
    {
        Client.Dispose();
        if (_hiddenStorage is not null)
        {
            // Back where the factory deletes it.
            Directory.Move(_hiddenStorage, _factory.StorageRoot);
            _hiddenStorage = null;
        }

        // Stops the host derived from it as well; a second call does nothing.
        await _factory.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await StopApiAsync();
        await Database.DisposeAsync();
    }

    // Every row of api_keys as JSON, by id.
    private async Task<JsonObject> KeyRowsAsync() =>
        JsonNode.Parse(await Database.ScalarAsync(
            "SELECT json_object_agg(id, row_to_json(api_keys))::text FROM api_keys")
            ?? "{}")!.AsObject();
}
