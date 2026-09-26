using LoDb.Desktop.Hosting;
using LoDb.Desktop.Launch;
using LoDb.Desktop.Shell;
using LoDb.Desktop.Smoke;

namespace LoDb.Desktop;

/// <summary>
/// Reads the command line, then either runs the smoke check or shows the window over the
/// loopback host.
/// </summary>
internal static class DesktopEntry
{
    /// <summary>Exit code of a command line the host cannot run with.</summary>
    public const int UsageError = 2;

    private const int Success = 0;

    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        DesktopLaunch launch;
        try
        {
            launch = DesktopArguments.Parse(
                args,
                Environment.GetEnvironmentVariable,
                DesktopBuild.Of(typeof(DesktopEntry).Assembly));
        }
        catch (ArgumentException exception)
        {
            error.WriteLine(exception.Message);
            return UsageError;
        }

        return launch.Mode == LaunchMode.Smoke ? RunSmoke(launch, output) : RunWindow(launch);
    }

    private static int RunSmoke(DesktopLaunch launch, TextWriter output) =>
        SmokeRun.RunAsync(launch, output, CancellationToken.None).GetAwaiter().GetResult();

    // Blocking waits keep the window on the main thread, which Cocoa and WebView2 require.
    private static int RunWindow(DesktopLaunch launch)
    {
        DataDirectory.Prepare(launch.Options.DataDirectory);
        var host = LoopbackHost.StartAsync(launch.Options, overrides: null, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var runner = host.Services.GetRequiredService<DesktopRunner>();
        if (!runner.TryRunWindow(host.Address))
        {
            // Browser fallback: the host serves the system browser until the process is ended.
            host.WaitForShutdownAsync().GetAwaiter().GetResult();
        }

        // Only reached where closing the window returns (not on macOS, spike report 4.1).
        host.DisposeAsync().AsTask().GetAwaiter().GetResult();
        return Success;
    }
}
