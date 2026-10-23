using LoDb.Api.Modules.Admin.Prerender.Demand.Capture;
using LoDb.Api.Modules.Admin.Prerender.Demand.Drain;
using LoDb.Api.Modules.Admin.Prerender.Demand.Http;
using LoDb.Api.Modules.Admin.Prerender.Demand.Queue;
using LoDb.Api.Modules.Admin.Prerender.Demand.Sharing;
using LoDb.Api.Modules.Admin.Prerender.Demand.Telemetry;
using LoDb.Api.Modules.Admin.Prerender.Demand.Validation;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Demand;

/// <summary>
/// The automatic queue of the pre-rendering: its settings, the slots it shares with the passes,
/// its reader of the SSR's refused pages and its <c>prerender-refused</c> client, its worker's
/// services and the admin's reader. Neither I/O nor throw: the workers under
/// <c>Workers/Prerender</c> resolve them per tick and sleep while the queue is off.
/// </summary>
internal static class DemandRegistration
{
    // Ten thousand paths of some sixty bytes, as JSON: far below, a bound on an answer that
    // is not one.
    private const int MaxReportBytes = 4 * 1024 * 1024;

    // The SSR answers from memory in some milliseconds: a slower one is a missing read.
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(5);

    public static IServiceCollection AddPrerenderDemand(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<DemandOptions>()
            .Bind(configuration.GetSection(DemandOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor
            .Singleton<IValidateOptions<DemandOptions>, DemandOptionsValidator>());
        services.TryAddSingleton<DemandActivation>();
        services.TryAddSingleton<RenderSlots>();
        services.TryAddSingleton<DemandMetrics>();
        services.TryAddSingleton<PageIndexCache>();
        services.TryAddSingleton<DemandValidator>();
        services.TryAddSingleton<DemandUpserts>();
        services.TryAddSingleton<DemandQueue>();
        services.TryAddSingleton<DemandRewrites>();
        services.TryAddSingleton<DemandPruner>();
        services.TryAddSingleton<DemandCollector>();
        services.TryAddSingleton<DemandPageWriter>();
        services.TryAddSingleton<DemandPageRunner>();
        services.TryAddSingleton<DemandDrain>();
        services.TryAddScoped<DemandReader>();
        services.TryAddSingleton<IRefusedPagesReader, RefusedPagesClient>();
        AddRefusedClient(services);
        return services;
    }

    // On the stack network alone: no redirect, no cookie, no log line per read.
    private static void AddRefusedClient(IServiceCollection services) =>
        services.AddHttpClient(RefusedPagesClient.ClientName)
            .ConfigureHttpClient(static client =>
            {
                client.Timeout = ReadTimeout;
                client.MaxResponseContentBufferSize = MaxReportBytes;
            })
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
            })
            .RemoveAllLoggers();
}
