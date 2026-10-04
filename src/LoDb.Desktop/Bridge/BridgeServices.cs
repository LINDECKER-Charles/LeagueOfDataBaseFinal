using LoDb.Desktop.Bridge.Commands;

namespace LoDb.Desktop.Bridge;

/// <summary>The bridge and its four commands.</summary>
internal static class BridgeServices
{
    public static IServiceCollection AddDesktopBridge(this IServiceCollection services)
    {
        services.AddSingleton<IBridgeCommand, OpenExternalCommand>();
        services.AddSingleton<IBridgeCommand, SaveFileCommand>();
        services.AddSingleton<IBridgeCommand, UpdateStateCommand>();
        services.AddSingleton<IBridgeCommand, ApplyUpdateCommand>();
        services.AddSingleton<DesktopBridge>();
        return services;
    }
}
