using System.Net;
using LoDb.Desktop.Hosting;
using LoDb.Desktop.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Desktop.Tests.Hosting;

public sealed class ShellEndpointsTests : IAsyncLifetime
{
    private const string Marker =
        "<head><script>window.__LODB_DESKTOP__=Object.freeze({version:\"9.8.7\"});</script>";

    private readonly TestFolder _folder = new();
    private DesktopTestHost _host = null!;

    public async ValueTask InitializeAsync() =>
        _host = await DesktopTestHost.StartAsync(
            new DesktopHostSetup
            {
                ApiOrigin = new Uri("http://127.0.0.1:1"),
                ShellDirectory = TestShell.CreateIn(_folder),
            },
            TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync()
    {
        await _host.DisposeAsync();
        _folder.Dispose();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/index.html")]
    [InlineData("/INDEX.HTML")]
    [InlineData("/fr/champions/103-ahri")]
    public async Task ServesTheIndexWithTheDesktopMarkerFirstInHead(string path)
    {
        using var response = await GetAsync(path);
        var page = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(Marker + "<meta charset=\"utf-8\">", page, StringComparison.Ordinal);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task HandsTheBridgeTransportToThePageOnceTheWindowRuns()
    {
        _host.Services.GetRequiredService<InjectedBridge>().Use(FakeShell.Transport);

        using var response = await GetAsync("/");
        var page = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains(
            "{version:\"9.8.7\",bridge:window.__testBridge}",
            page,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ServesTheFilesOfTheShell()
    {
        using var response = await GetAsync("/main.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            TestShell.Script,
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("/desktop/unknown")]
    [InlineData("/missing.js")]
    public async Task Answers404RatherThanAPageToUnknownLocalPaths(string path)
    {
        using var response = await GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void EscapesTheVersionSoThatItCannotCloseTheScript()
    {
        var script = ShellIndex.MarkerScript("1.0</script><script>alert(1)//", transport: null);

        Assert.Equal(1, CountOf(script, "</script>"));
        Assert.DoesNotContain("<script>alert", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Answers404WithoutAShellBuild()
    {
        await using var bare = await DesktopTestHost.StartAsync(
            new DesktopHostSetup { ApiOrigin = new Uri("http://127.0.0.1:1") },
            TestContext.Current.CancellationToken);

        using var response = await bare.Client.GetAsync(
            new Uri("/", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private Task<HttpResponseMessage> GetAsync(string path) =>
        _host.Client.GetAsync(
            new Uri(path, UriKind.Relative),
            TestContext.Current.CancellationToken);

    private static int CountOf(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length)
        / value.Length;
}
