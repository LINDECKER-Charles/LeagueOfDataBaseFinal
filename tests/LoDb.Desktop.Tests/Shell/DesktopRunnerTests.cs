using System.Text.Json.Nodes;
using LoDb.Desktop.Shell;
using LoDb.Desktop.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LoDb.Desktop.Tests.Shell;

public sealed class DesktopRunnerTests : IAsyncLifetime
{
    // Far above the few milliseconds a close takes; a deadlock never ends.
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(10);

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

    private DesktopRunner Runner => _host.Services.GetRequiredService<DesktopRunner>();

    [Fact]
    public async Task RunsTheWindowOnTheLoopbackPageWithTheBridge()
    {
        Assert.True(Runner.TryRunWindow(_host.Host.Address));
        var window = _host.Setup.Shell.Window!;
        var page = await _host.Client.GetStringAsync(
            new Uri("/", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(_host.Host.Address, window.Address);
        Assert.Equal("webview", Path.GetFileName(window.WebViewDirectory));
        Assert.True(Directory.Exists(Path.GetDirectoryName(window.WebViewDirectory)));
        Assert.Contains($"bridge:{FakeShell.Transport}", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostsTheReplyOfEachPageMessageBackToThePage()
    {
        Runner.TryRunWindow(_host.Host.Address);

        await _host.Setup.Shell.Window!.OnMessage("{\"id\":\"u1\",\"type\":\"updateState\"}");

        var reply = JsonNode.Parse(Assert.Single(_host.Setup.Shell.Posted))!;
        Assert.Equal("u1", (string?)reply["id"]);
        Assert.True((bool?)reply["ok"]);
    }

    [Fact]
    public void StopsTheHostAsTheWindowCloses()
    {
        var lifetime = _host.Services.GetRequiredService<IHostApplicationLifetime>();
        Runner.TryRunWindow(_host.Host.Address);

        _host.Setup.Shell.Window!.OnClosing();

        Assert.True(lifetime.ApplicationStopping.IsCancellationRequested);
    }

    [Fact]
    public void ClosesTheWindowWhenTheHostStopsOnASignal()
    {
        var lifetime = _host.Services.GetRequiredService<IHostApplicationLifetime>();
        var shell = _host.Setup.Shell;

        // The signal arrives on its own thread while the window, open until closed, holds
        // the main one.
        shell.WhileOpen = window =>
        {
            _ = Task.Run(lifetime.StopApplication);
            SpinWait.SpinUntil(() => shell.IsClosed, CloseTimeout);
        };
        Runner.TryRunWindow(_host.Host.Address);

        Assert.True(shell.IsClosed);
    }

    [Fact]
    public void DoesNotCloseAgainAWindowTheUserClosed()
    {
        _host.Setup.Shell.WhileOpen = window => window.OnClosing();

        Runner.TryRunWindow(_host.Host.Address);

        Assert.False(_host.Setup.Shell.IsClosed);
    }

    [Fact]
    public async Task FallsBackToTheSystemBrowserWithoutABridgeWhenTheWebViewFails()
    {
        _host.Setup.Shell.StartFailure = new DllNotFoundException("Photino.Native");

        var hasRun = Runner.TryRunWindow(_host.Host.Address);
        var page = await _host.Client.GetStringAsync(
            new Uri("/", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.False(hasRun);
        Assert.Equal(_host.Host.Address, Assert.Single(_host.Setup.Browser.Opened));
        Assert.DoesNotContain("bridge:", page, StringComparison.Ordinal);
    }
}
