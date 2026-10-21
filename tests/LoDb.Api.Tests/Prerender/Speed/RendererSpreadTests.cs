using System.Net;
using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Rendering.Renderers;
using LoDb.Api.Modules.Admin.Prerender.Units;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Api.Tests.Prerender.Speed;

/// <summary>
/// Several renderers: the pass resolves their shared name every 30 s and sends each request
/// to the least busy, taking turns among equals, the public Host header untouched; with one,
/// nothing changes. The pages in flight follow the pace, within their bound.
/// </summary>
public sealed class RendererSpreadTests
{
    private static readonly IPAddress First = IPAddress.Parse("172.30.0.21");
    private static readonly IPAddress Second = IPAddress.Parse("172.30.0.22");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void LeastBusyRendererIsChosenAndEqualsTakeTurns()
    {
        var slots = new RendererSlots();
        IPAddress[] both = [First, Second];

        var turns = new[] { slots.Take(both), slots.Take(both) };
        slots.Release(First);
        var freed = slots.Take(both);

        Assert.Equal(new[] { First, Second }, turns);
        Assert.Equal(First, freed);
    }

    [Fact]
    public async Task SingleRendererKeepsTheNameAsConfigured()
    {
        var dns = new FakeDns([First, Second]);
        using var addresses = Addresses(renderers: 1, dns, new FakeTimeProvider());

        Assert.Empty(await addresses.CurrentAsync(Token));
        Assert.Equal(0, dns.Resolutions);
    }

    [Fact]
    public async Task NameIsResolvedEveryThirtySecondsAndAFailureKeepsWhatWasKnown()
    {
        var clock = new FakeTimeProvider();
        var dns = new FakeDns([First]);
        using var addresses = Addresses(renderers: 2, dns, clock);

        var first = await addresses.CurrentAsync(Token);
        dns.Answer = [First, Second];
        var cached = await addresses.CurrentAsync(Token);
        clock.Advance(RendererAddresses.Refresh);
        var refreshed = await addresses.CurrentAsync(Token);
        dns.Answer = [];
        clock.Advance(RendererAddresses.Refresh);
        var kept = await addresses.CurrentAsync(Token);

        Assert.Equal(new[] { First }, first);
        Assert.Equal(new[] { First }, cached);
        Assert.Equal(new[] { First, Second }, refreshed);
        Assert.Equal(new[] { First, Second }, kept);
        Assert.Equal(3, dns.Resolutions);
    }

    [Fact]
    public async Task RequestGoesToARendererAddressWithThePublicHostKept()
    {
        var dns = new FakeDns([First, Second]);
        using var addresses = Addresses(renderers: 2, dns, new FakeTimeProvider());
        var sent = new List<(Uri Url, string? Host)>();
        using var handler = new RendererSpreadHandler(addresses, new RendererSlots())
        {
            InnerHandler = new Recorder(sent),
        };
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://lodb-web-ssr-prerender:4000"),
        };
        client.DefaultRequestHeaders.Host = "league-of-data-base.com";

        using var page = await client.GetAsync(Relative("/fr/16.5.1/items"), Token);
        using var next = await client.GetAsync(Relative("/fr/16.5.1/runes"), Token);

        Assert.Equal(
            new (Uri, string?)[]
            {
                (new Uri("http://172.30.0.21:4000/fr/16.5.1/items"), "league-of-data-base.com"),
                (new Uri("http://172.30.0.22:4000/fr/16.5.1/runes"), "league-of-data-base.com"),
            },
            sent);
    }

    [Theory]
    [InlineData(0, 24, 1)]
    [InlineData(30, 24, 1)]
    [InlineData(240, 24, 6)]
    [InlineData(600, 24, 15)]
    [InlineData(1200, 24, 24)]
    [InlineData(600, 2, 2)]
    public void PagesInFlightFollowThePaceWithinTheirBound(int pace, int most, int expected)
    {
        Assert.Equal(expected, InFlightWidth.Of(pace, most));
    }

    private static Uri Relative(string path) => new(path, UriKind.Relative);

    private static RendererAddresses Addresses(int renderers, FakeDns dns, TimeProvider clock) =>
        new(
            Microsoft.Extensions.Options.Options.Create(new PrerenderOptions
            {
                SsrBaseUrl = "http://lodb-web-ssr-prerender:4000",
                Renderers = renderers,
            }),
            dns,
            clock);

    private sealed class FakeDns(IReadOnlyList<IPAddress> answer) : IRendererDns
    {
        public IReadOnlyList<IPAddress> Answer { get; set; } = answer;

        public int Resolutions { get; private set; }

        public Task<IReadOnlyList<IPAddress>> ResolveAsync(
            string host,
            CancellationToken cancellationToken)
        {
            Assert.Equal("lodb-web-ssr-prerender", host);
            Resolutions++;
            return Task.FromResult(Answer);
        }
    }

    private sealed class Recorder(List<(Uri Url, string? Host)> sent) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            sent.Add((request.RequestUri!, request.Headers.Host));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
