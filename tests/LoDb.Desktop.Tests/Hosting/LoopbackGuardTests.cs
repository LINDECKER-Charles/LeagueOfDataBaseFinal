using System.Net;
using LoDb.Desktop.Tests.Support;

namespace LoDb.Desktop.Tests.Hosting;

public sealed class LoopbackGuardTests : IAsyncLifetime
{
    private DesktopTestHost _host = null!;

    public async ValueTask InitializeAsync() =>
        _host = await DesktopTestHost.StartAsync(
            new DesktopHostSetup { ApiOrigin = new Uri("http://127.0.0.1:1") },
            TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task ServesItsOwnOrigin()
    {
        using var response = await _host.Client.GetAsync(
            new Uri("/desktop/health", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("rebound.example")]
    public async Task RefusesAnotherHostNameOnItsPort(string hostName)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/desktop/auth/logout");
        request.Headers.Host = $"{hostName}:{_host.Host.Address.Port}";

        using var response = await _host.Client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("Origin", "https://evil.example")]
    [InlineData("Origin", "null")]
    [InlineData("Sec-Fetch-Site", "cross-site")]
    [InlineData("Sec-Fetch-Site", "same-site")]
    public async Task RefusesRequestsOfOtherPages(string header, string value)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/desktop/auth/logout");
        request.Headers.Add(header, value);

        using var response = await _host.Client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AcceptsRequestsOfItsOwnPage()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/desktop/auth/logout");
        request.Headers.Add("Origin", _host.Host.Address.GetLeftPart(UriPartial.Authority));
        request.Headers.Add("Sec-Fetch-Site", "same-origin");

        using var response = await _host.Client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task LetsTheRedirectOfGoogleReachItsCallback()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/desktop/auth/google/callback?code=c&state=unknown");
        request.Headers.Add("Sec-Fetch-Site", "cross-site");

        using var response = await _host.Client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        // Past the guard, the state decides: no flow runs, so the callback refuses it.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
