using LoDb.Desktop.Launch;
using LoDb.Desktop.Lifecycle;
using LoDb.Desktop.Updates.Engine;
using LoDb.Desktop.Updates.Engine.Signing;

namespace LoDb.Desktop.Updates;

/// <summary>
/// The app's updates over Velopack, registered by the loopback host, with the feed key the
/// build trusts.
/// </summary>
internal static class UpdateServices
{
    public static IServiceCollection AddDesktopUpdates(this IServiceCollection services)
    {
        services.AddSingleton(UpdateArguments.Parse(
            Environment.GetCommandLineArgs(),
            Environment.GetEnvironmentVariable));
        services.AddSingleton(_ => new FeedSignature(
            UpdateFeeds.TrustedKeys(DesktopBuild.Of(typeof(UpdateServices).Assembly))));
        services.AddSingleton<SignedFeeds>();
        services.AddSingleton<IUpdateEngine, VelopackEngine>();
        services.AddSingleton<VelopackUpdates>();
        services.AddSingleton<IDesktopUpdates>(
            provider => provider.GetRequiredService<VelopackUpdates>());
        services.AddHostedService<UpdateLoop>();
        return services;
    }
}
