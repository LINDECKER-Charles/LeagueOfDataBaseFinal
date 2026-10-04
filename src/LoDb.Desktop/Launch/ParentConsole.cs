using System.Runtime.InteropServices;

namespace LoDb.Desktop.Launch;

/// <summary>
/// Lets <c>--smoke</c> print in the terminal that started it on Windows, where a WinExe
/// starts without a console. Redirected output needs nothing and is left alone.
/// </summary>
internal static class ParentConsole
{
    private const int AttachParentProcess = -1;

    public static void Attach()
    {
        if (OperatingSystem.IsWindows() && !Console.IsOutputRedirected)
        {
            // Fails when started from Explorer, where there is no console to attach to: the
            // output is then lost, as it would be anyway.
            _ = AttachConsole(AttachParentProcess);
        }
    }

    // DllImport rather than LibraryImport, whose generated code would need unsafe blocks for
    // a signature that has nothing to marshal.
    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int AttachConsole(int processId);
}
