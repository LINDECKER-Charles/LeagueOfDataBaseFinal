using LoDb.Api.Modules.Admin.Prerender.Revision.Drain;
using LoDb.Api.Modules.Admin.Prerender.Revision.Http;
using LoDb.Api.Modules.Admin.Prerender.Revision.Producing;
using LoDb.Api.Modules.Admin.Prerender.Revision.Queue;
using LoDb.Api.Modules.Admin.Prerender.Revision.Telemetry;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Api.Modules.Admin.Prerender.Revision;

/// <summary>
/// The queue of the pages of the closed states (<c>prerender_revision</c>): its producer, which
/// the promotion and the reconciliation call, its drain and the admin's reader. Neither I/O nor
/// throw: the worker under <c>Workers/Prerender</c> resolves the drain per tick and sleeps while
/// the store is not configured.
/// </summary>
internal static class RevisionRegistration
{
    public static IServiceCollection AddPrerenderRevision(this IServiceCollection services)
    {
        services.TryAddSingleton<RevisionMetrics>();
        services.TryAddSingleton<RevisionProducer>();
        services.TryAddSingleton<RevisionQueue>();
        services.TryAddSingleton<RevisionPageRunner>();
        services.TryAddSingleton<RevisionDrain>();
        services.TryAddScoped<RevisionReader>();
        return services;
    }
}
