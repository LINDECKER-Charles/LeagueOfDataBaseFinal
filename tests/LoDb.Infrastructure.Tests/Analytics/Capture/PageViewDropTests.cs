using System.Diagnostics.Metrics;
using System.Net;
using LoDb.Infrastructure.Analytics;
using LoDb.Infrastructure.Analytics.Capture;
using LoDb.Infrastructure.Persistence.Analytics;
using LoDb.Infrastructure.Tests.Analytics.Support;
using LoDb.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Infrastructure.Tests.Analytics.Capture;

/// <summary>
/// The views dropped before they reach a table, over a database that never answers: a full
/// queue refuses the next view, a batch that cannot be written is dropped whole, each counted
/// by its reason and logged without a trace of the visitor.
/// </summary>
public sealed class PageViewDropTests
{
    private const string Dropped = "lodb.analytics.views.dropped";

    private static readonly DateTimeOffset Now = new(2026, 9, 26, 23, 59, 58, TimeSpan.Zero);

    [Fact]
    public async Task FullQueueRefusesTheNextView()
    {
        await using var services = AnalyticsServices.Build(
            ApiFactory.UnreachablePostgres,
            new FakeTimeProvider(Now),
            static options => options.QueueCapacity = 1);
        using var dropped = Collect(services);
        var capture = services.GetRequiredService<IPageViewCapture>();

        Assert.True(capture.TryCapture(View("/fr/items")));
        Assert.False(capture.TryCapture(View("/fr/runes")));

        var drop = Assert.Single(dropped.GetMeasurementSnapshot());
        Assert.Equal(AnalyticsMetrics.QueueFull, drop.Tags[AnalyticsMetrics.ReasonTag]);
    }

    [Fact]
    public async Task DatabaseOutageDropsTheBatchWithoutClientDataInTheLogs()
    {
        await using var services = AnalyticsServices.Build(
            ApiFactory.UnreachablePostgres,
            new FakeTimeProvider(Now));
        using var dropped = Collect(services);
        var capture = services.GetRequiredService<IPageViewCapture>();
        capture.TryCapture(View("/fr/items"));
        capture.TryCapture(View("/fr/runes"));

        var written = await services.GetRequiredService<IPageViewPump>()
            .FlushAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, written);
        var drop = Assert.Single(dropped.GetMeasurementSnapshot());
        Assert.Equal(2, drop.Value);
        Assert.Equal(AnalyticsMetrics.WriteFailed, drop.Tags[AnalyticsMetrics.ReasonTag]);
        Assert.All(services.GetFakeLogCollector().GetSnapshot(), static log =>
        {
            var text = log.Message + log.Exception;
            Assert.DoesNotContain("203.0.113.7", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Firefox", text, StringComparison.Ordinal);
            Assert.DoesNotContain("/fr/", text, StringComparison.Ordinal);
        });
    }

    private static MetricCollector<long> Collect(IServiceProvider services) =>
        new(services.GetRequiredService<IMeterFactory>(), AnalyticsMetrics.MeterName, Dropped);

    private static CapturedView View(string target) => new()
    {
        OccurredAt = Now,
        Origin = AnalyticsCaptureOrigin.ServedPage,
        Target = target,
        Host = "league-of-data-base.com",
        ClientAddress = IPAddress.Parse("203.0.113.7"),
        UserAgent = "Mozilla/5.0 (X11; Linux x86_64; rv:140.0) Firefox/140.0",
        Referer = "https://www.google.com/search?q=lodb",
    };
}
