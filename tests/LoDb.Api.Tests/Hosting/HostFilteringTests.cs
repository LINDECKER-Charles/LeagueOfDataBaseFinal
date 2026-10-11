using LoDb.Api.Hosting;
using LoDb.Api.Tests.Hosting.Support;
using Microsoft.AspNetCore.Http;

namespace LoDb.Api.Tests.Hosting;

/// <summary>
/// <c>AllowedHosts</c>: a served API answers its public hosts, its stack alias and the
/// loopback, and refuses any other <c>Host</c> with a 400; the scrape of the metrics port,
/// which names the container by its address, is never filtered.
/// </summary>
/// <remarks>No test changes the host's state: one host serves them all.</remarks>
public sealed class HostFilteringTests(FilteredHostsApi api) : IClassFixture<FilteredHostsApi>
{
    private const int ApiPort = 8080;
    private const int MetricsPort = 9464;

    [Theory]
    [InlineData("example.test")]
    [InlineData("lodb-api")]
    public async Task ListedHostsReachTheApi(string host)
    {
        var response = await SendAsync(new HostString(host, ApiPort), "/healthz");

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
    }

    [Theory]
    [InlineData("evil.example")]
    [InlineData("example.test.evil.example")]
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

    private async Task<HttpResponse> SendAsync(HostString host, string path)
    {
        var sent = await api.Factory.Server.SendAsync(
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
