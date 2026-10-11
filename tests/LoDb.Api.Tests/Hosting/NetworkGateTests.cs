using System.Net;
using LoDb.Api.Tests.Hosting.Support;
using Microsoft.AspNetCore.Http;

namespace LoDb.Api.Tests.Hosting;

/// <summary>
/// The API port answers the stack's own network and the loopback only: a container of another
/// network the API shares (the scrape's), forwarded headers or not, gets a 404 and nothing
/// else, while the metrics port stays open to the scrape.
/// </summary>
/// <remarks>
/// One host serves them all: the refusals they leave counted only add to the one the
/// metrics test makes itself.
/// </remarks>
public sealed class NetworkGateTests(GatedApi api) : IClassFixture<GatedApi>
{
    private const string ForwardedFor = "X-Forwarded-For";
    private const int ApiPort = 8080;
    private const int MetricsPort = 9464;
    private const string Health = "/healthz";
    private const string Metrics = "/metrics";
    private const string Localhost = "localhost";

    // A visitor's address, as nginx forwards it.
    private const string PublicClient = "203.0.113.9";

    // A container of a network shared with other projects, such as the scrape's.
    private static readonly IPAddress SharedNetworkPeer = IPAddress.Parse("172.20.0.3");

    // nginx or the renderer, on the stack's own network.
    private static readonly IPAddress StackPeer = IPAddress.Parse("10.231.1.7");

    [Theory]
    [InlineData("10.231.1.7")]
    [InlineData("::1")]
    public async Task StackAndLoopbackReachTheApi(string address)
    {
        var response = await CallApiAsync(IPAddress.Parse(address));

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
    }

    [Theory]
    [InlineData("10.231.2.7")]
    [InlineData("fd00::12")]
    public async Task AnyOtherPeerGetsANotFound(string address)
    {
        var response = await CallApiAsync(IPAddress.Parse(address));

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Empty(await ReadAsync(response));
    }

    [Fact]
    public async Task ForwardedHeadersDoNotOpenTheGate()
    {
        var response = await CallApiAsync(
            SharedNetworkPeer,
            static request => request.Headers[ForwardedFor] = "10.231.1.7");

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
    }

    // The gate judges the connection before the forwarded headers replace its address: judged
    // after them, every visitor nginx forwards would be refused.
    [Fact]
    public async Task ProxiedVisitorFromTheStackReachesTheApi()
    {
        var response = await CallApiAsync(
            StackPeer,
            static request => request.Headers[ForwardedFor] = PublicClient);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
    }

    [Fact]
    public async Task MetricsPortIsLeftToTheScrapeAndCountsTheRefusals()
    {
        await CallApiAsync(SharedNetworkPeer);

        var scrape = await SendAsync(static context =>
        {
            context.Connection.RemoteIpAddress = SharedNetworkPeer;
            context.Request.Host = new HostString(Localhost, MetricsPort);
            context.Request.Path = Metrics;
        });

        Assert.Equal(StatusCodes.Status200OK, scrape.StatusCode);
        Assert.Contains(
            "lodb_network_gate_refusals_total{",
            await ReadAsync(scrape),
            StringComparison.Ordinal);
    }

    private Task<HttpResponse> CallApiAsync(
        IPAddress? address,
        Action<HttpRequest>? withRequest = null) =>
        SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = address;
            context.Request.Host = new HostString(Localhost, ApiPort);
            context.Request.Path = Health;
            withRequest?.Invoke(context.Request);
        });

    private static async Task<string> ReadAsync(HttpResponse response)
    {
        using var reader = new StreamReader(response.Body);
        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponse> SendAsync(Action<HttpContext> configure)
    {
        var sent = await api.Factory.Server.SendAsync(
            request =>
            {
                request.Request.Method = HttpMethods.Get;
                configure(request);
            },
            TestContext.Current.CancellationToken);
        return sent.Response;
    }
}
