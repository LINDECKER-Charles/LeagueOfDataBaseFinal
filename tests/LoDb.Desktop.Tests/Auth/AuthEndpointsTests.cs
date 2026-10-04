using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using LoDb.Desktop.Auth.Tokens;
using LoDb.Desktop.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Desktop.Tests.Auth;

public sealed class AuthEndpointsTests : IAsyncLifetime
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
    public async Task LoginAnswers204AndKeepsTheTokensInTheHost()
    {
        using var response = await _host.LoginAsync(FakeApi.Password);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var session = await _host.ReadSessionAsync();

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(body);
        Assert.True((bool?)session["signedIn"]);
        Assert.False((bool?)session["remembered"]);
        Assert.Equal("idle", (string?)session["google"]);
        Assert.DoesNotContain("access-", session.ToJsonString(), StringComparison.Ordinal);
        Assert.DoesNotContain("refresh-", session.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoginRelaysTheRefusalOfTheAPI()
    {
        using var response = await _host.LoginAsync("wrong-password");
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("invalid-credentials", (string?)problem!["code"]);
        Assert.False((bool?)(await _host.ReadSessionAsync())["signedIn"]);
    }

    [Fact]
    public async Task LoginAnswers502WhenTheAPICannotBeReached()
    {
        // Port 1 on loopback: nothing listens, the connection is refused at once.
        await using var offline = await DesktopTestHost.StartAsync(
            new DesktopHostSetup { ApiOrigin = new Uri("http://127.0.0.1:1") },
            TestContext.Current.CancellationToken);

        using var response = await offline.LoginAsync(FakeApi.Password);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("api-unreachable", (string?)problem!["code"]);
    }

    [Fact]
    public async Task RememberMeKeepsTheRefreshTokenEncryptedOnDisk()
    {
        using var response = await _host.LoginAsync(FakeApi.Password, rememberMe: true);
        var vault = _host.Services.GetRequiredService<RefreshTokenVault>();
        var saved = await File.ReadAllTextAsync(
            vault.FilePath,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain("refresh-", saved, StringComparison.Ordinal);
        Assert.True((bool?)(await _host.ReadSessionAsync())["remembered"]);
    }

    [Fact]
    public async Task LogoutForgetsTheSessionAndTheSavedToken()
    {
        using var login = await _host.LoginAsync(FakeApi.Password, rememberMe: true);
        var vault = _host.Services.GetRequiredService<RefreshTokenVault>();

        using var response = await _host.Client.PostAsync(
            new Uri("/desktop/auth/logout", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(File.Exists(vault.FilePath));
        Assert.False((bool?)(await _host.ReadSessionAsync())["signedIn"]);
        Assert.Equal(string.Empty, (string?)(await _host.EchoAsync())["authorization"]);
    }

    [Fact]
    public async Task RenewsTheAccessTokenOnceItExpires()
    {
        using var login = await _host.LoginAsync(FakeApi.Password);
        var first = (string?)(await _host.EchoAsync())["authorization"];

        _host.Setup.Time.Advance(TimeSpan.FromSeconds(FakeApi.AccessLifetimeSeconds));
        var renewed = (string?)(await _host.EchoAsync())["authorization"];

        Assert.Equal("Bearer access-1", first);
        Assert.Equal("Bearer access-2", renewed);
        Assert.Equal(["refresh-1"], _api.RefreshesReceived);
    }

    [Fact]
    public async Task RenewsOnceForConcurrentRequests()
    {
        using var login = await _host.LoginAsync(FakeApi.Password);
        _host.Setup.Time.Advance(TimeSpan.FromSeconds(FakeApi.AccessLifetimeSeconds));

        var echoes = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => _host.EchoAsync()));

        Assert.All(echoes, echo => Assert.Equal("Bearer access-2", (string?)echo["authorization"]));
        Assert.Single(_api.RefreshesReceived);
    }

    [Fact]
    public async Task EndsTheSessionWhenTheAPIRefusesTheRefresh()
    {
        using var login = await _host.LoginAsync(FakeApi.Password, rememberMe: true);
        _api.IsRefreshRefused = true;
        _host.Setup.Time.Advance(TimeSpan.FromSeconds(FakeApi.AccessLifetimeSeconds));

        var received = await _host.EchoAsync();

        Assert.Equal(string.Empty, (string?)received["authorization"]);
        Assert.False((bool?)(await _host.ReadSessionAsync())["signedIn"]);
        Assert.False(File.Exists(_host.Services.GetRequiredService<RefreshTokenVault>().FilePath));
    }

    [Fact]
    public async Task ARememberedSessionSurvivesARestart()
    {
        using var folder = new TestFolder();
        var setup = new DesktopHostSetup { ApiOrigin = _api.Origin, DataDirectory = folder.Path };
        await using (var first = await DesktopTestHost.StartAsync(
            setup,
            TestContext.Current.CancellationToken))
        {
            using var login = await first.LoginAsync(FakeApi.Password, rememberMe: true);
        }

        await using var second = await DesktopTestHost.StartAsync(
            setup with { Shell = new FakeShell(), Browser = new FakeBrowser() },
            TestContext.Current.CancellationToken);
        var session = await second.ReadSessionAsync();
        var received = await second.EchoAsync();

        Assert.True((bool?)session["signedIn"]);
        Assert.True((bool?)session["remembered"]);
        Assert.Equal("Bearer access-2", (string?)received["authorization"]);
        Assert.Equal(["refresh-1"], _api.RefreshesReceived);
    }

    [Fact]
    public async Task ASessionWithoutRememberMeEndsWithTheApp()
    {
        using var folder = new TestFolder();
        var setup = new DesktopHostSetup { ApiOrigin = _api.Origin, DataDirectory = folder.Path };
        await using (var first = await DesktopTestHost.StartAsync(
            setup,
            TestContext.Current.CancellationToken))
        {
            using var login = await first.LoginAsync(FakeApi.Password);
        }

        await using var second = await DesktopTestHost.StartAsync(
            setup with { Shell = new FakeShell(), Browser = new FakeBrowser() },
            TestContext.Current.CancellationToken);

        Assert.False((bool?)(await second.ReadSessionAsync())["signedIn"]);
    }
}
