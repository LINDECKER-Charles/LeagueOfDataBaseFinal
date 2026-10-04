using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Seo.Sitemaps;

/// <summary>The sitemap routes, at the root of the site: nginx sends them to the API.</summary>
internal static class SitemapEndpoints
{
    private const string Folder = "/sitemaps";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                SitemapFile.IndexPath,
                static (
                    [FromServices] SitemapPublisher publisher,
                    HttpRequest request,
                    CancellationToken aborted) => publisher.IndexAsync(request, aborted))
            .ExcludeFromDescription();

        endpoints.MapGet(
                $"{Folder}/{{file}}",
                static (
                    [FromServices] SitemapPublisher publisher,
                    string file,
                    CancellationToken aborted) => publisher.PreviousAsync(file, aborted))
            .ExcludeFromDescription();

        endpoints.MapGet(
                $"{Folder}/{{locale}}/{{file}}",
                static (
                    [FromServices] SitemapPublisher publisher,
                    HttpRequest request,
                    string locale,
                    string file) => publisher.LocaleAsync(request, locale, file))
            .ExcludeFromDescription();
    }
}
