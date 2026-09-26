using LoDb.Desktop.Auth;
using LoDb.Desktop.Bridge;
using LoDb.Desktop.Lifecycle;
using LoDb.Desktop.Proxy;
using LoDb.Desktop.Shell;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Desktop.Hosting;

/// <summary>The services and the pipeline of the loopback host.</summary>
internal static class DesktopServices
{
    public static IServiceCollection AddDesktopHost(
        this IServiceCollection services,
        DesktopOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<InjectedBridge>();
        services.AddSingleton<ShellIndex>();
        services.AddApiProxy();
        services.AddDesktopAuth(options);
        services.AddDesktopBridge();
        services.AddDesktopShell();

        // Updates/ (L9.4) adds its registration on the line above this one; the default
        // below then stays out, and development builds keep reporting "no update".
        services.TryAddSingleton<IDesktopUpdates, NoDesktopUpdates>();
        return services;
    }

    public static void UseDesktopHost(this WebApplication application)
    {
        application.UseMiddleware<LoopbackGuard>();
        application.UseShellFiles();
        application.MapApiProxy();
        application.MapDesktopAuth();
        application.MapShell();
    }
}
