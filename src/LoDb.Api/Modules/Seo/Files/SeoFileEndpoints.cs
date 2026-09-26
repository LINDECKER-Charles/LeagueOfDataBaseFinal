using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Seo.Files;

/// <summary>The crawler files, at the root of the site: nginx sends them to the API.</summary>
internal static class SeoFileEndpoints
{
    public const string RobotsPath = "/robots.txt";
    public const string LlmsPath = "/llms.txt";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                RobotsPath,
                static ([FromServices] SeoFilesPublisher publisher, HttpRequest request) =>
                    publisher.Robots(request))
            .ExcludeFromDescription();

        endpoints.MapGet(
                LlmsPath,
                static ([FromServices] SeoFilesPublisher publisher, HttpRequest request) =>
                    publisher.LlmsAsync(request))
            .ExcludeFromDescription();
    }
}
