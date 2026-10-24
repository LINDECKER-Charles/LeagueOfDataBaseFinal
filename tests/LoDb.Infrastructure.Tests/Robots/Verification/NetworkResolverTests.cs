using System.Diagnostics.Metrics;
using System.Net;
using LoDb.Infrastructure.Robots;
using LoDb.Infrastructure.Robots.Verification;
using LoDb.Infrastructure.Tests.Robots.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Infrastructure.Tests.Robots.Verification;

/// <summary>
/// The addresses of a batch are looked up once within the budget, the others put off, and
/// a failure is tried again only once its shorter lifetime is over.
/// </summary>
public sealed class NetworkResolverTests : IDisposable
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly FakeTimeProvider _time =
        new(new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));

    private readonly ServiceProvider _services = new ServiceCollection()
        .AddMetrics()
        .BuildServiceProvider();

    private readonly MetricCollector<long> _lookups;
    private readonly FakeReverseDns _dns = new FakeReverseDns()
        .Confirmed("66.249.66.1", "crawl-66-249-66-1.googlebot.com")
        .Failing("203.0.113.9");

    private readonly RobotsMetrics _metrics;

    public NetworkResolverTests()
    {
        var factory = _services.GetRequiredService<IMeterFactory>();
        _metrics = new RobotsMetrics(factory);
        _lookups = new MetricCollector<long>(
            factory,
            RobotsMetrics.MeterName,
            "lodb.robots.dns.lookups");
    }

    [Fact]
    public async Task AKnownAnswerIsNotAskedAgain()
    {
        var resolver = Resolver(lookupsPerBatch: 10);
        var address = IPAddress.Parse("66.249.66.1");

        var first = await resolver.ResolveAsync([address, address], Cancellation);
        var second = await resolver.ResolveAsync([address], Cancellation);

        Assert.Equal("crawl-66-249-66-1.googlebot.com", first[address].Host);
        Assert.True(second[address].IsConfirmed);
        Assert.Equal(1, _dns.ReverseQuestions);
        var lookup = Assert.Single(_lookups.GetMeasurementSnapshot());
        Assert.Equal("confirmed", lookup.Tags[RobotsMetrics.OutcomeTag]);
    }

    [Fact]
    public async Task TheBudgetPutsTheOthersOff()
    {
        var resolver = Resolver(lookupsPerBatch: 1);

        var found = await resolver.ResolveAsync(
            [IPAddress.Parse("198.51.100.1"), IPAddress.Parse("198.51.100.2")],
            Cancellation);

        Assert.Single(found);
        Assert.Equal(ReverseLookup.NoName, found.Values.Single());
    }

    [Fact]
    public async Task AFailureIsTriedAgainAfterItsLifetime()
    {
        var resolver = Resolver(lookupsPerBatch: 10);
        var address = IPAddress.Parse("203.0.113.9");

        var failed = await resolver.ResolveAsync([address], Cancellation);
        await resolver.ResolveAsync([address], Cancellation);
        _time.Advance(TimeSpan.FromMinutes(11));
        await resolver.ResolveAsync([address], Cancellation);

        Assert.True(failed[address].HasFailed);
        Assert.Equal(2, _dns.ReverseQuestions);
    }

    public void Dispose()
    {
        _lookups.Dispose();
        _services.Dispose();
    }

    private NetworkResolver Resolver(int lookupsPerBatch) => new(
        _dns,
        Options.Create(new RobotsOptions
        {
            DnsLookupsPerBatch = lookupsPerBatch,
            DnsFailureLifetime = TimeSpan.FromMinutes(10),
        }),
        _metrics,
        _time);
}
