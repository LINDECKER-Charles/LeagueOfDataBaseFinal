using LoDb.Desktop.Launch;
using Velopack;

namespace LoDb.Desktop;

/// <summary>Entry point of the desktop app.</summary>
internal static class Program
{
    /// <remarks>
    /// <c>[STAThread]</c> is not optional: without it WebView2 renders nothing, with no error
    /// (ADR 0007). Velopack runs first, as an install or update hook ends the process there.
    /// </remarks>
    [STAThread]
    private static int Main(string[] args)
    {
        VelopackApp.Build().Run();
        if (args.Contains(DesktopArguments.Smoke, StringComparer.Ordinal))
        {
            // Before the first use of Console.Out, which binds to whatever console exists then.
            ParentConsole.Attach();
        }

        return DesktopEntry.Run(args, Console.Out, Console.Error);
    }
}
