using LoDb.Desktop.Shell.Photino;

namespace LoDb.Desktop.Shell;

/// <summary>The window, the system browser, and the runner that ties them to the host.</summary>
internal static class ShellServices
{
    public static IServiceCollection AddDesktopShell(this IServiceCollection services)
    {
        // The single line that names the implementation: the plan B swaps it here.
        services.AddSingleton<IDesktopShell, PhotinoShell>();
        services.AddSingleton<ISystemBrowser, SystemBrowser>();
        services.AddSingleton<DesktopRunner>();
        return services;
    }
}
