using LoDb.Api.Modules.Admin.Prerender.Pace.Readings;
using LoDb.Api.Modules.Admin.Prerender.Pace.Telemetry;
using LoDb.Api.Modules.Admin.Prerender.Pace.Windows;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Api.Modules.Admin.Prerender.Pace;

/// <summary>
/// The regulator of the pass's pace, its readers and its <c>prerender-load</c> client. Neither
/// I/O nor throw: the readers read at each tick of <c>PrerenderPaceWorker</c>, never before.
/// </summary>
internal static class PaceRegistration
{
    // An SSR answers its load in a few milliseconds: a slower one is a missing reading.
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(2);

    // The load is some hundreds of bytes: a bound on an answer that is not one.
    private const int MaxLoadBytes = 64 * 1024;

    public static IServiceCollection AddPrerenderPace(this IServiceCollection services)
    {
        services.TryAddSingleton<PaceMetrics>();
        services.TryAddSingleton<PaceRegulator>();
        services.TryAddSingleton<PassRenderTimes>();
        services.TryAddSingleton<PaceSourceWatch>();
        services.TryAddSingleton<PaceSampler>();
        services.TryAddSingleton<IHostCpuReader, ProcStatReader>();
        services.TryAddSingleton<ICgroupCpuReader, CgroupCpuReader>();
        services.TryAddSingleton<ISsrLoadReader, SsrLoadClient>();
        AddLoadClient(services);
        return services;
    }

    // On the stack network alone: no redirect, no cookie, no log line per read.
    private static void AddLoadClient(IServiceCollection services) =>
        services.AddHttpClient(SsrLoadClient.ClientName)
            .ConfigureHttpClient(static client =>
            {
                client.Timeout = LoadTimeout;
                client.MaxResponseContentBufferSize = MaxLoadBytes;
            })
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
            })
            .RemoveAllLoggers();
}
