using LoDb.Api.Modules.Admin.Prerender.Http.History;
using LoDb.Api.Modules.Admin.Prerender.Http.Storage;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Http;

/// <summary>
/// Registrations of the pre-rendering's admin API: its report, its desk and the prices of the
/// store. Neither I/O nor throw: the pass's own services come from
/// <see cref="PrerenderRegistration.AddAdminPrerender"/>.
/// </summary>
internal static class PrerenderHttpRegistration
{
    public static IServiceCollection AddAdminPrerenderHttp(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<PrerenderPriceOptions>()
            .Bind(configuration.GetSection(PrerenderPriceOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor
            .Singleton<IValidateOptions<PrerenderPriceOptions>, PrerenderPriceOptionsValidator>());
        services.TryAddScoped<PassHistory>();
        services.TryAddScoped<StorageCensus>();
        services.TryAddScoped<PrerenderJournal>();
        services.TryAddScoped<PrerenderDesk>();
        return services;
    }
}
