using System.Net;
using LoDb.Infrastructure.Analytics;
using LoDb.Infrastructure.Analytics.Capture;
using LoDb.Infrastructure.Audience;
using LoDb.Infrastructure.Audience.Umami;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Tests.Audience.Support;
using LoDb.Infrastructure.Tests.Persistence;
using LoDb.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Infrastructure.Tests.Audience;

/// <summary>
/// The pull of the audience: Umami's views per path become views per entity and day in
/// <c>audience_entity_daily</c>, through the catalog's reading of the addresses, the recent
/// days rewritten whole; the read-only account only ever reaches the configured host.
/// </summary>
public sealed class AudiencePullTests(PostgresContainerFixture postgres)
    : MigratedDatabase(postgres)
{
    private const string Host = "stats.example.test";
    private const string WebsiteId = "deee1958-0f6c-4a32-a327-aad389836c3b";
    private const string Username = "trends-reader";
    private const string Password = "reader-password";
    private const string Gauge = "lodb.audience.vues_tirees";

    // 09:30 UTC: the pull covers the 11th and the 10th, its gauge the hour from 08:00 to 09:00.
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 9, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 11);
    private static readonly DateOnly Yesterday = new(2026, 10, 10);

    private readonly FakeTimeProvider _time = new(Now);
    private readonly FakeUmami _umami = new();
    private ServiceProvider? _services;

    private ServiceProvider Services => _services ??= Build(pageSize: 2);

    [Fact]
    public async Task ViewsOfTheDetailPagesBecomeViewsPerEntityAndDay()
    {
        _umami.Days[Today] =
        [
            (CatalogPages.AhriFr, 50, 20),
            (CatalogPages.Sword, 30, 12),
            (CatalogPages.AhriEn, 10, 6),
            (CatalogPages.Champions, 900, 400),
            (CatalogPages.Missing, 7, 7),
            (CatalogPages.Pinned, 5, 5),
            ("/fr/champions/103-ahri?crafted=1", 1000, 1000),
        ];
        _umami.Days[Yesterday] = [(CatalogPages.Sword, 4, 3)];

        var rows = await Pull().PullAsync(Cancellation);

        Assert.Equal(3, rows);
        Assert.Equal(
            [
                "2026-10-10 item:1036 4 3",
                "2026-10-11 champion:Ahri 60 26",
                "2026-10-11 item:1036 30 12",
            ],
            await ReadAsync());
    }

    [Fact]
    public async Task APullRewritesItsDaysWhole()
    {
        _umami.Days[Today] = [(CatalogPages.AhriFr, 50, 20), (CatalogPages.Sword, 30, 12)];
        await Pull().PullAsync(Cancellation);
        _umami.Days[Today] = [(CatalogPages.Sword, 31, 13)];

        await Pull().PullAsync(Cancellation);

        Assert.Equal(["2026-10-11 item:1036 31 13"], await ReadAsync());
    }

    [Fact]
    public async Task UmamiIsReadAPageAtATimeUntilAShortOne()
    {
        _umami.Days[Today] =
        [
            (CatalogPages.AhriFr, 50, 20),
            (CatalogPages.Champions, 40, 30),
            (CatalogPages.Sword, 30, 12),
            (CatalogPages.AhriEn, 10, 6),
        ];

        await Pull().PullAsync(Cancellation);

        var offsets = _umami.Requests
            .Where(static request => request.Url.AbsolutePath.EndsWith(
                "/metrics/expanded",
                StringComparison.Ordinal))
            .Select(static request => request.Url.Query)
            .Where(static query => query.Contains(
                $"startAt={UmamiWindow.Of(Today).StartAt}",
                StringComparison.Ordinal))
            .Select(static query => query.Split("offset=")[1]);
        Assert.Equal(["0", "2", "4"], offsets);
        Assert.Equal(["2026-10-11 champion:Ahri 60 26", "2026-10-11 item:1036 30 12"],
            await ReadAsync());
    }

    [Fact]
    public async Task TheGaugeTakesTheViewsOfTheLastClosedHour()
    {
        _umami.SiteViews = 1234;
        using var gauge = new MetricCollector<long>(
            Services.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>(),
            AudienceMetrics.MeterName,
            Gauge);
        gauge.RecordObservableInstruments();
        Assert.Null(gauge.LastMeasurement);

        await Pull().PullAsync(Cancellation);
        gauge.RecordObservableInstruments();

        Assert.Equal(1234, gauge.LastMeasurement?.Value);
        var stats = Assert.Single(
            _umami.Requests,
            static request =>
                request.Url.AbsolutePath.EndsWith("/stats", StringComparison.Ordinal));
        var eight = new DateTimeOffset(2026, 10, 11, 8, 0, 0, TimeSpan.Zero);
        var nine = eight.AddHours(1);
        Assert.Equal(
            $"?startAt={eight.ToUnixTimeMilliseconds()}&endAt={nine.ToUnixTimeMilliseconds() - 1}",
            stats.Url.Query);
    }

    [Fact]
    public async Task TheAccountSignsInOnTheConfiguredHostAlone()
    {
        await Pull().PullAsync(Cancellation);

        Assert.All(_umami.Requests, static request =>
        {
            Assert.Equal(Uri.UriSchemeHttps, request.Url.Scheme);
            Assert.Equal(Host, request.Url.Host);
        });
        var signIn = _umami.Requests[0];
        Assert.Equal(HttpMethod.Post, signIn.Method);
        Assert.Contains($"\"username\":\"{Username}\"", signIn.Body, StringComparison.Ordinal);
        Assert.All(_umami.Requests.Skip(1), static request =>
            Assert.Equal($"Bearer {FakeUmami.Token}", request.Authorization));
        Assert.All(_umami.Requests.Skip(1), static request => Assert.StartsWith(
            $"/api/websites/{WebsiteId}/",
            request.Url.AbsolutePath,
            StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("https://elsewhere.example.test/api/auth/login")]
    [InlineData("http://stats.example.test/api/auth/login")]
    public async Task TheClientRefusesAnyOtherDestination(string url)
    {
        var client = Services.GetRequiredService<IHttpClientFactory>()
            .CreateClient(UmamiClient.ClientName);

        await Assert.ThrowsAsync<UmamiException>(() => client.GetAsync(new Uri(url), Cancellation));

        Assert.Empty(_umami.Requests);
    }

    [Fact]
    public async Task ARefusedAccountWritesNothing()
    {
        _umami.SignInStatus = HttpStatusCode.Unauthorized;
        _umami.Days[Today] = [(CatalogPages.AhriFr, 50, 20)];

        var error = await Assert.ThrowsAsync<UmamiException>(() => Pull().PullAsync(Cancellation));

        Assert.Contains("401", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, error.Message, StringComparison.Ordinal);
        Assert.Empty(await ReadAsync());
    }

    protected override async ValueTask DisposeServicesAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }

        _umami.Dispose();
    }

    private IAudiencePull Pull() => Services.GetRequiredService<IAudiencePull>();

    private Task<IReadOnlyList<string>> ReadAsync() =>
        Database.QueryAsync(
            """
            SELECT concat_ws(' ', day::text, entity, views, visitors)
              FROM audience_entity_daily ORDER BY day, entity
            """,
            Cancellation);

    private ServiceProvider Build(int pageSize)
    {
        var section = AudienceOptions.SectionName + ":";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:LoDb"] = Database.ConnectionString,
                [section + nameof(AudienceOptions.Host)] = Host,
                [section + nameof(AudienceOptions.WebsiteId)] = WebsiteId,
                [section + nameof(AudienceOptions.Username)] = Username,
                [section + nameof(AudienceOptions.Password)] = Password,
                [section + nameof(AudienceOptions.PageSize)] = pageSize.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
            })
            .Build();
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton<TimeProvider>(_time)
            .AddFakeLogging()
            .AddMetrics()
            .AddLoDbPersistence(configuration)
            .AddLoDbAnalytics(configuration)
            .AddSingleton<ITrackedPageResolver, CatalogPages>();
        services.AddHttpClient(UmamiClient.ClientName)
            .ConfigurePrimaryHttpMessageHandler(() => _umami);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
