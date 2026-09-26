using LoDb.Desktop.Hosting;
using LoDb.Desktop.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Desktop.Tests.Support;

/// <summary>
/// The real loopback host of the app, with a fake window, browser and clock, a temporary
/// data folder, and a client that talks to it as the WebView does.
/// </summary>
internal sealed class DesktopTestHost : IAsyncDisposable
{
    public const string Version = "9.8.7";
    public const string GoogleClientId = "desktop-client.apps.googleusercontent.com";

    private readonly TestFolder? _ownedFolder;

    private DesktopTestHost(LoopbackHost host, DesktopHostSetup setup, TestFolder? ownedFolder)
    {
        Host = host;
        Setup = setup;
        _ownedFolder = ownedFolder;
        var handler = new SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = false };
        Client = new HttpClient(handler) { BaseAddress = host.Address };
    }

    public LoopbackHost Host { get; }

    public DesktopHostSetup Setup { get; }

    /// <summary>A client of the loopback origin: its Host header is the host's own.</summary>
    public HttpClient Client { get; }

    public IServiceProvider Services => Host.Services;

    public static async Task<DesktopTestHost> StartAsync(
        DesktopHostSetup setup,
        CancellationToken cancellationToken)
    {
        var ownedFolder = setup.DataDirectory is null ? new TestFolder() : null;
        var options = new DesktopOptions
        {
            Version = Version,
            Channel = "stable",
            ApiOrigin = setup.ApiOrigin,
            ShellDirectory = setup.ShellDirectory ?? Path.Combine(Path.GetTempPath(), "no-shell"),
            DataDirectory = setup.DataDirectory ?? ownedFolder!.Path,
            GoogleClientId = setup.GoogleClientId,
        };
        var host = await LoopbackHost.StartAsync(
            options,
            services =>
            {
                services.AddSingleton<IDesktopShell>(setup.Shell);
                services.AddSingleton<ISystemBrowser>(setup.Browser);
                services.AddSingleton<TimeProvider>(setup.Time);
            },
            cancellationToken);
        return new DesktopTestHost(host, setup, ownedFolder);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Host.DisposeAsync();
        _ownedFolder?.Dispose();
    }
}
