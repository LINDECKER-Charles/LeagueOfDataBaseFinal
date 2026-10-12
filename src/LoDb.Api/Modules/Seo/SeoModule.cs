using LoDb.Api.Modules.Seo.Files;
using LoDb.Api.Modules.Seo.IndexNow;
using LoDb.Api.Modules.Seo.Sitemaps;
using LoDb.Ingestion.Pipeline.Versions;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Seo;

/// <summary>
/// SEO module: sitemaps, robots and llms files built from the catalog, and the IndexNow
/// submission of the pages a promoted patch changed.
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
        AddIndexNow(services);
        return services;
    }

    // No redirect followed nor cookie kept: the submissions only go to the one endpoint.
    private static void AddIndexNow(IServiceCollection services)
    {
        services.TryAddSingleton<IndexNowClient>();
        services.TryAddEnumerable(ServiceDescriptor
            .Singleton<IPromotionListener, IndexNowSubmitter>());
        services.AddHttpClient(IndexNowClient.ClientName)
            .ConfigureHttpClient(static client => client.Timeout = IndexNowClient.Timeout)
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
            })
            .RemoveAllLoggers();
    }

    public static IEndpointRouteBuilder MapSeo(this IEndpointRouteBuilder endpoints)
    {
        SitemapEndpoints.Map(endpoints);
        SeoFileEndpoints.Map(endpoints);
        return endpoints;
    }
}
