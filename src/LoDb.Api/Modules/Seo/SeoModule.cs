namespace LoDb.Api.Modules.Seo;

/// <summary>
/// SEO module: sitemaps, robots and llms files built from the catalog.
/// </summary>
/// <remarks>
/// Program.cs calls both methods from the start, so the module's owner fills this file
/// without touching a shared one. Neither method may do I/O or throw.
/// </remarks>
internal static class SeoModule
{
    public static IServiceCollection AddSeo(
        this IServiceCollection services,
        IConfiguration configuration) => services;

    public static IEndpointRouteBuilder MapSeo(this IEndpointRouteBuilder endpoints) =>
        endpoints;
}
