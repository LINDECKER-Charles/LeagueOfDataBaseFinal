using LoDb.Desktop.Auth;
using LoDb.Desktop.Bridge;
using LoDb.Desktop.Lifecycle;
using LoDb.Desktop.Proxy;
using LoDb.Desktop.Shell;
using LoDb.Desktop.Updates;
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

        // Updates/ (L9.4) registers Velopack on the line below; the default after it then
        // stays out, and a build Velopack did not install keeps reporting "no update".
        services.AddDesktopUpdates();
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
