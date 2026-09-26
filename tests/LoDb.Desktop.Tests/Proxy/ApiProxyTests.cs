using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using LoDb.Desktop.Tests.Support;

namespace LoDb.Desktop.Tests.Proxy;

public sealed class ApiProxyTests : IAsyncLifetime
{
    private FakeApi _api = null!;
    private DesktopTestHost _host = null!;

    public async ValueTask InitializeAsync()
    {
        _api = await FakeApi.StartAsync(TestContext.Current.CancellationToken);
        _host = await DesktopTestHost.StartAsync(
            new DesktopHostSetup { ApiOrigin = _api.Origin },
            TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.DisposeAsync();
        await _api.DisposeAsync();
    }

    [Fact]
    public async Task ReplacesTheCredentialsAndTheOriginOfThePageByTheAppHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, FakeApi.EchoPath);
        request.Headers.Add("Cookie", "lod_theme=zaun");
        request.Headers.Add("Authorization", "Bearer forged-by-the-page");
        request.Headers.Add("Origin", _host.Host.Address.GetLeftPart(UriPartial.Authority));
        request.Headers.Add("Referer", _host.Host.Address.ToString());
        request.Headers.Add("X-LoDb-Client", "web/0.0.1");

        using var response = await _host.Client.SendAsync(
            request,
            TestContext.Current.CancellationToken);
        var received = await response.Content.ReadFromJsonAsync<JsonObject>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(string.Empty, (string?)received!["authorization"]);
        Assert.Equal(string.Empty, (string?)received["cookie"]);
        Assert.Equal(string.Empty, (string?)received["origin"]);
        Assert.Equal(string.Empty, (string?)received["referer"]);
        Assert.Equal($"desktop/{DesktopTestHost.Version}", (string?)received["client"]);
    }

    [Fact]
    public async Task KeepsTheCookiesOfTheAPIAwayFromTheWebView()
    {
        using var response = await _host.Client.GetAsync(
            new Uri(FakeApi.EchoPath, UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task AddsTheAccessTokenOfTheSignedInUser()
    {
        using var login = await _host.LoginAsync(FakeApi.Password);

        var received = await _host.EchoAsync();

        Assert.Equal($"Bearer {_api.LastAccessToken}", (string?)received["authorization"]);
    }

    [Fact]
    public async Task RelaysUnsafeRequestsOfItsPageWithoutTheirOrigin()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, FakeApi.EchoPath);
        request.Headers.Add("Origin", _host.Host.Address.GetLeftPart(UriPartial.Authority));

        using var response = await _host.Client.SendAsync(
            request,
            TestContext.Current.CancellationToken);
        var received = await response.Content.ReadFromJsonAsync<JsonObject>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(string.Empty, (string?)received!["origin"]);
    }
}
