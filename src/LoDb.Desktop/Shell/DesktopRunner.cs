using LoDb.Desktop.Bridge;
using LoDb.Desktop.Hosting;
using LoDb.Desktop.Lifecycle;

namespace LoDb.Desktop.Shell;

/// <summary>
/// Runs the window over the loopback host, relays the bridge, and falls back to the system
/// browser when the WebView cannot start (ADR 0007).
/// </summary>
internal sealed partial class DesktopRunner(
    IDesktopShell shell,
    DesktopBridge bridge,
    InjectedBridge injectedBridge,
    ISystemBrowser browser,
    IDesktopUpdates updates,
    DesktopOptions options,
    IHostApplicationLifetime lifetime,
    ILogger<DesktopRunner> logger)
{
    private const string Title = "League of Data Base";

    /// <returns>
    /// True once the window has run and closed; false when the WebView could not start and
    /// the system browser shows the app instead, without a bridge.
    /// </returns>
    public bool TryRunWindow(Uri address)
    {
        injectedBridge.Use(shell.BridgeTransport);
        var hasClosed = false;

        // A signal stops the host: the window must not stay open on a dead page. Queued, not
        // called: the lifetime runs this under its lock, and the close waits for the closing
        // handler, whose StopApplication would wait for that lock on the UI thread.
        using var stopping = lifetime.ApplicationStopping.Register(() =>
        {
            if (!Volatile.Read(ref hasClosed))
            {
                ThreadPool.QueueUserWorkItem(_ => CloseWindow());
            }
        });
        try
        {
            shell.Run(WindowAt(address, () =>
            {
                Volatile.Write(ref hasClosed, true);
                Close();
            }));
            return true;
        }
        catch (Exception exception) when (!Volatile.Read(ref hasClosed))
        {
            LogShellFailed(logger, exception);
            FallBackToBrowser(address);
            return false;
        }
    }

    private ShellWindow WindowAt(Uri address, Action onClosing) => new()
    {
        Address = address,
        Title = Title,
        WebViewDirectory = Path.Combine(options.DataDirectory, DataDirectory.WebViewFolder),
        IsDevToolsEnabled = options.IsDevToolsEnabled,
        OnMessage = RelayAsync,
        OnClosing = onClosing,
    };

    private void CloseWindow()
    {
        try
        {
            shell.Close();
        }
        catch (Exception exception)
        {
            // On a pool thread: a failure here would otherwise vanish.
            LogCloseFailed(logger, exception);
        }
    }

    private void FallBackToBrowser(Uri address)
    {
        injectedBridge.Use(null);
        if (!browser.Open(address))
        {
            // Nothing can show the app: stop rather than linger without a window.
            lifetime.StopApplication();
        }
    }

    // The end of the app's life: on macOS nothing runs after the closing handler.
    private void Close()
    {
        updates.PrepareExit();
        lifetime.StopApplication();
    }

    private async Task RelayAsync(string message)
    {
        try
        {
            var reply = await bridge.HandleAsync(message, lifetime.ApplicationStopping);
            if (reply is not null)
            {
                shell.Post(reply);
            }
        }
        catch (Exception exception)
        {
            // Off the UI thread and unobserved: a failure here would otherwise vanish.
            LogRelayFailed(logger, exception);
        }
    }

    [LoggerMessage(
        EventName = "desktop.shell.failed",
        Level = LogLevel.Error,
        Message = "The WebView could not start; the system browser shows the app instead.")]
    private static partial void LogShellFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventName = "desktop.shell.close_failed",
        Level = LogLevel.Warning,
        Message = "The window could not be closed as the host stopped.")]
    private static partial void LogCloseFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventName = "desktop.bridge.relay_failed",
        Level = LogLevel.Warning,
        Message = "A bridge reply could not be relayed to the page.")]
    private static partial void LogRelayFailed(ILogger logger, Exception exception);
}
