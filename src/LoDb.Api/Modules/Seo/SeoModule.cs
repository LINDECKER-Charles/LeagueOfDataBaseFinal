using LoDb.Api.Modules.Seo.Files;
using LoDb.Api.Modules.Seo.Sitemaps;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Seo;

/// <summary>
/// SEO module: sitemaps, robots and llms files built from the catalog.
/// </summary>
/// <remarks>
/// Program.cs calls both methods from the start, so the module's owner fills this file
/// without touching a shared one. Neither method may do I/O or throw. The routes sit at the
/// root of the site, outside the OpenAPI documents: nginx's <c>server.d/seo.conf</c> sends
/// them here.
/// </remarks>
internal static class SeoModule
{
    public static IServiceCollection AddSeo(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<SeoOptions>()
            .Bind(configuration.GetSection(SeoOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor
            .Singleton<IValidateOptions<SeoOptions>, SeoOptionsValidator>());
        services.TryAddSingleton<SiteOrigin>();
        services.TryAddSingleton<CrawlerCatalog>();
        services.TryAddSingleton<SitemapPublisher>();
        services.TryAddSingleton<SeoFilesPublisher>();
        return services;
    }

    public static IEndpointRouteBuilder MapSeo(this IEndpointRouteBuilder endpoints)
    {
        SitemapEndpoints.Map(endpoints);
        SeoFileEndpoints.Map(endpoints);
        return endpoints;
    }
}
