using LoDb.Api.Hosting;
using LoDb.Testing;
using Microsoft.AspNetCore.Http;

namespace LoDb.Api.Tests.Hosting;

/// <summary>
/// <c>AllowedHosts</c>: a served API answers its public hosts, its stack alias and the
/// loopback, and refuses any other <c>Host</c> with a 400; the scrape of the metrics port,
/// which names the container by its address, is never filtered.
/// </summary>
public sealed class HostFilteringTests : IAsyncDisposable
{
    private const string Hosts =
        "example.test, example.fr,api.example.test;lodb-api ,localhost,127.0.0.1";

    private const int ApiPort = 8080;
    private const int MetricsPort = 9464;

    private readonly ApiFactory _factory = new()
    {
        Settings = new Dictionary<string, string?> { [HostFilteringSetup.Key] = Hosts },
    };

    [Theory]
    [InlineData("example.test")]
    [InlineData("EXAMPLE.FR")]
    [InlineData("api.example.test")]
    [InlineData("lodb-api")]
    [InlineData("127.0.0.1")]
    public async Task ListedHostsReachTheApi(string host)
    {
        var response = await SendAsync(new HostString(host, ApiPort), "/healthz");

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
    }

    [Theory]
    [InlineData("evil.example")]
    [InlineData("example.test.evil.example")]
    [InlineData("10.231.1.4")]
    public async Task AnyOtherHostGetsABadRequest(string host)
    {
        var response = await SendAsync(new HostString(host, ApiPort), "/healthz");

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TheScrapeOfTheMetricsPortIsNotFiltered()
    {
        var response = await SendAsync(new HostString("172.20.0.3", MetricsPort), "/metrics");

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
    }

    [Theory]
    [InlineData(null, new[] { "*" })]
    [InlineData(" ; , ", new[] { "*" })]
    [InlineData("a.test, b.test;A.TEST", new[] { "a.test", "b.test" })]
    [InlineData("a.test b.test,c.test", new[] { "a.test", "b.test", "c.test" })]
    public void SettingListsItsHostsOrAllowsEvery(string? setting, string[] hosts) =>
        Assert.Equal(hosts, HostFilteringSetup.Parse(setting));

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private async Task<HttpResponse> SendAsync(HostString host, string path)
    {
        var sent = await _factory.Server.SendAsync(
            context =>
            {
                context.Request.Method = HttpMethods.Get;
                context.Request.Host = host;
                context.Request.Path = path;
            },
            TestContext.Current.CancellationToken);
        return sent.Response;
    }
}
