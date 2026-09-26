using LoDb.Desktop.Lifecycle;
using LoDb.Desktop.Updates.Engine;

namespace LoDb.Desktop.Updates;

/// <summary>The app's updates over Velopack, registered by the loopback host.</summary>
internal static class UpdateServices
{
    public static IServiceCollection AddDesktopUpdates(this IServiceCollection services)
    {
        services.AddSingleton(UpdateArguments.Parse(
            Environment.GetCommandLineArgs(),
            Environment.GetEnvironmentVariable));
        services.AddSingleton<IUpdateEngine, VelopackEngine>();
        services.AddSingleton<VelopackUpdates>();
        services.AddSingleton<IDesktopUpdates>(
            provider => provider.GetRequiredService<VelopackUpdates>());
        services.AddHostedService<UpdateLoop>();
        return services;
    }
}
