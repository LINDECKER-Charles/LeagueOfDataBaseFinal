using System.Diagnostics.Metrics;
using System.Net;
using LoDb.Infrastructure.Analytics;
using LoDb.Infrastructure.Analytics.Capture;
using LoDb.Infrastructure.Persistence.Analytics;
using LoDb.Infrastructure.Persistence.Analytics.Partitions;
using LoDb.Infrastructure.Tests.Analytics.Support;
using LoDb.Infrastructure.Tests.Persistence;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Infrastructure.Tests.Analytics.Capture;

/// <summary>
/// From the capture queue to <c>analytics_event</c>: views resolved off the request, written
/// by batch in one copy, the missing partitions created on the way, and whatever fails
/// dropped and counted without a trace of the visitor in the logs.
/// </summary>
public sealed class PageViewPumpTests(PostgresContainerFixture postgres)
    : MigratedDatabase(postgres)
{
    private const string Host = "league-of-data-base.com";
    private const string Firefox = "Mozilla/5.0 (X11; Linux x86_64; rv:140.0) Firefox/140.0";

    private static readonly DateTimeOffset Now = new(2026, 9, 26, 23, 59, 58, TimeSpan.Zero);
    private static readonly IPAddress Client = IPAddress.Parse("203.0.113.7");

    private readonly FakeTimeProvider _time = new(Now);
    private ServiceProvider? _services;

    private ServiceProvider Services =>
        _services ??= AnalyticsServices.Build(Database.ConnectionString, _time);

    [Fact]
    public async Task ArrivalAndTwoNavigationsAreThreeViews()
    {
        var capture = Services.GetRequiredService<IPageViewCapture>();

        Assert.True(capture.TryCapture(View("/fr/", AnalyticsCaptureOrigin.ServedPage)));
        Assert.True(capture.TryCapture(View("/fr/items", AnalyticsCaptureOrigin.Navigation, 1)));
        Assert.True(capture.TryCapture(View("/fr/runes", AnalyticsCaptureOrigin.Navigation, 2)));
        var written = await Pump(Services).FlushAsync(Cancellation);

        Assert.Equal(3, written);
        var rows = await ReadAsync();
        Assert.Equal(["/fr/", "/fr/items", "/fr/runes"], rows.Select(static row => row.Path));
        Assert.Equal(("www.google.com", "search"), (rows[0].RefererHost, rows[0].RefererSource));
        Assert.All(rows.Skip(1), static row =>
            Assert.Equal((Host, "internal"), (row.RefererHost, row.RefererSource)));
        Assert.Single(rows.Select(static row => row.Visitor).Distinct());
        Assert.All(rows, static row =>
        {
            Assert.Equal(("Firefox", "Linux", "desktop"), (row.Browser, row.Os, row.Device));
            Assert.Equal("203.0.113.7", row.Ip);
        });
    }

    [Fact]
    public async Task MissingPartitionsAreCreatedAndTheBatchWrittenAgain()
    {
        var capture = Services.GetRequiredService<IPageViewCapture>();
        capture.TryCapture(View("/fr/items", AnalyticsCaptureOrigin.ServedPage));
        capture.TryCapture(View("/fr/runes", AnalyticsCaptureOrigin.Navigation, 5));

        Assert.Equal(2, await Pump(Services).FlushAsync(Cancellation));

        Assert.Equal(
            [new DateOnly(2026, 9, 26), new DateOnly(2026, 9, 27)],
            await Services.GetRequiredService<IAnalyticsPartitions>().ListAsync(Cancellation));
    }

    [Fact]
    public async Task QueueIsDrainedBatchAfterBatch()
    {
        _services = AnalyticsServices.Build(
            Database.ConnectionString,
            _time,
            static options => options.BatchSize = 2);
        var queue = Services.GetRequiredService<PageViewQueue>();
        for (var second = 0; second < 5; second++)
        {
            queue.TryCapture(View("/fr/items", AnalyticsCaptureOrigin.Navigation, -second));
        }

        Assert.Equal(2, queue.TakeBatch().Count);
        Assert.Equal(3, await Pump(Services).FlushAsync(Cancellation));
        Assert.Equal(3, (await ReadAsync()).Count);
    }

    [Fact]
    public async Task UnresolvedViewIsDroppedAlone()
    {
        using var dropped = Collect("lodb.analytics.views.dropped");
        var capture = Services.GetRequiredService<IPageViewCapture>();
        capture.TryCapture(View(StubPageResolver.Failing, AnalyticsCaptureOrigin.ServedPage));
        capture.TryCapture(View(StubPageResolver.Skipped, AnalyticsCaptureOrigin.ServedPage));
        capture.TryCapture(View("/fr/items", AnalyticsCaptureOrigin.ServedPage));

        Assert.Equal(1, await Pump(Services).FlushAsync(Cancellation));

        var drop = Assert.Single(dropped.GetMeasurementSnapshot());
        Assert.Equal(AnalyticsMetrics.Unresolved, drop.Tags[AnalyticsMetrics.ReasonTag]);
        AssertNoClientDataLogged();
    }

    [Fact]
    public void FullQueueRefusesTheNextView()
    {
        _services = AnalyticsServices.Build(
            Database.ConnectionString,
            _time,
            static options => options.QueueCapacity = 1);
        using var dropped = Collect("lodb.analytics.views.dropped");
        var capture = Services.GetRequiredService<IPageViewCapture>();

        Assert.True(capture.TryCapture(View("/fr/items", AnalyticsCaptureOrigin.ServedPage)));
        Assert.False(capture.TryCapture(View("/fr/runes", AnalyticsCaptureOrigin.ServedPage)));

        var drop = Assert.Single(dropped.GetMeasurementSnapshot());
        Assert.Equal(AnalyticsMetrics.QueueFull, drop.Tags[AnalyticsMetrics.ReasonTag]);
    }

    [Fact]
    public async Task DatabaseOutageDropsTheBatchWithoutClientDataInTheLogs()
    {
        await using var outage = AnalyticsServices.Build(ApiFactory.UnreachablePostgres, _time);
        using var dropped = new MetricCollector<long>(
            outage.GetRequiredService<IMeterFactory>(),
            AnalyticsMetrics.MeterName,
            "lodb.analytics.views.dropped");
        var capture = outage.GetRequiredService<IPageViewCapture>();
        capture.TryCapture(View("/fr/items", AnalyticsCaptureOrigin.ServedPage));
        capture.TryCapture(View("/fr/runes", AnalyticsCaptureOrigin.Navigation));

        Assert.Equal(0, await Pump(outage).FlushAsync(Cancellation));

        var drop = Assert.Single(dropped.GetMeasurementSnapshot());
        Assert.Equal(2, drop.Value);
        Assert.Equal(AnalyticsMetrics.WriteFailed, drop.Tags[AnalyticsMetrics.ReasonTag]);
        AssertNoClientDataLogged(outage);
    }

    protected override async ValueTask DisposeServicesAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }
    }

    private static IPageViewPump Pump(IServiceProvider services) =>
        services.GetRequiredService<IPageViewPump>();

    private static CapturedView View(string target, AnalyticsCaptureOrigin origin, int after = 0) =>
        new()
        {
            OccurredAt = Now.AddSeconds(after),
            Origin = origin,
            Target = target,
            Host = Host,
            ClientAddress = Client,
            UserAgent = Firefox,
            Referer = "https://www.google.com/search?q=lodb",
        };

    private MetricCollector<long> Collect(string instrument) =>
        new(
            Services.GetRequiredService<IMeterFactory>(),
            AnalyticsMetrics.MeterName,
            instrument);

    private void AssertNoClientDataLogged(IServiceProvider? services = null)
    {
        var logs = (services ?? Services).GetFakeLogCollector().GetSnapshot();
        Assert.All(logs, static log =>
        {
            var text = log.Message + log.Exception;
            Assert.DoesNotContain("203.0.113.7", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Firefox", text, StringComparison.Ordinal);
            Assert.DoesNotContain("/fr/", text, StringComparison.Ordinal);
        });
    }

    private async Task<List<AnalyticsEvent>> ReadAsync()
    {
        await using var context = Database.CreateContext();
        return await context.AnalyticsEvents
            .AsNoTracking()
            .OrderBy(static view => view.OccurredAt)
            .ToListAsync(Cancellation);
    }
}
