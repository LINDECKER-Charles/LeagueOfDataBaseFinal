using System.Net;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;

namespace LoDb.Desktop.Hosting;

/// <summary>
/// The Kestrel of the app, on 127.0.0.1 and a port the OS picks: it serves the shell build,
/// the token endpoints and the <c>/api</c> proxy to the WebView (ADR 0007).
/// </summary>
internal sealed partial class LoopbackHost : IAsyncDisposable
{
    /// <summary>
    /// Bound on the stop: the WebView's keep-alive connections would otherwise hold it until
    /// the host's own timeout (spike report, 4.3).
    /// </summary>
    public static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(3);

    private const int AnyFreePort = 0;

    private readonly WebApplication _application;
    private int _isDisposed;

    private LoopbackHost(WebApplication application, Uri address)
    {
        _application = application;
        Address = address;
    }

    /// <summary>Root of the loopback site, such as <c>http://127.0.0.1:52817/</c>.</summary>
    public Uri Address { get; }

    public IServiceProvider Services => _application.Services;

    /// <param name="options">Settings of this run.</param>
    /// <param name="overrides">Replaces services after the host's own (tests).</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    public static async Task<LoopbackHost> StartAsync(
        DesktopOptions options,
        Action<IServiceCollection>? overrides,
        CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(LoopbackHost).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.WebHost.ConfigureKestrel(
            kestrel => kestrel.Listen(IPAddress.Loopback, AnyFreePort));
        builder.Services.Configure<HostOptions>(host => host.ShutdownTimeout = StopTimeout);
        DesktopLogging.Configure(builder.Logging);
        builder.Services.AddDesktopHost(options);
        overrides?.Invoke(builder.Services);

        var application = builder.Build();
        application.UseDesktopHost();
        await application.StartAsync(cancellationToken);
        var address = ReadAddress(application);
        LogStarted(application.Logger, address);
        return new LoopbackHost(application, address);
    }

    /// <summary>Waits for a stop requested elsewhere (the lifetime, a signal).</summary>
    public Task WaitForShutdownAsync() => _application.WaitForShutdownAsync();

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
        {
            return;
        }

        using var bounded = new CancellationTokenSource(StopTimeout);
        try
        {
            await _application.StopAsync(bounded.Token);
        }
        catch (OperationCanceledException)
        {
            // Past the bound, open connections are dropped: nothing waits on them.
        }

        await _application.DisposeAsync();
    }

    private static Uri ReadAddress(WebApplication application)
    {
        var bound = application.Services
            .GetRequiredService<IServer>()
            .Features
            .GetRequiredFeature<IServerAddressesFeature>()
            .Addresses
            .Single();
        var port = new Uri(bound).Port;
        return new UriBuilder(Uri.UriSchemeHttp, IPAddress.Loopback.ToString(), port).Uri;
    }

    [LoggerMessage(
        EventName = "desktop.host.started",
        Level = LogLevel.Information,
        Message = "The loopback host listens on {Address}.")]
    private static partial void LogStarted(ILogger logger, Uri address);
}
