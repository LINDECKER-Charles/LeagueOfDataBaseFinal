using System.ComponentModel;
using System.Diagnostics;

namespace LoDb.Desktop.Shell;

/// <summary>
/// Opens a URL with the OS's handler: ShellExecute, <c>open</c> or <c>xdg-open</c>.
/// </summary>
internal sealed partial class SystemBrowser(ILogger<SystemBrowser> logger) : ISystemBrowser
{
    public bool Open(Uri address)
    {
        try
        {
            // The escaped absolute form: nothing in it can be read as a second argument.
            using var process = Process.Start(
                new ProcessStartInfo(address.AbsoluteUri) { UseShellExecute = true });
            return true;
        }
        catch (Exception exception) when (exception is Win32Exception
            or InvalidOperationException
            or PlatformNotSupportedException)
        {
            // The host only: the query may carry an OAuth state.
            LogOpenFailed(logger, address.Host, exception);
            return false;
        }
    }

    [LoggerMessage(
        EventName = "desktop.browser.open_failed",
        Level = LogLevel.Warning,
        Message = "The system browser could not be opened on {Host}.")]
    private static partial void LogOpenFailed(ILogger logger, string host, Exception exception);
}
