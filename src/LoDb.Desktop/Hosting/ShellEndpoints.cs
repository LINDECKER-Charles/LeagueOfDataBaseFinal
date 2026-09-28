using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;

namespace LoDb.Desktop.Hosting;

/// <summary>Serves the shell build: its files, and its index with the marker.</summary>
internal static class ShellEndpoints
{
    private const string IndexPath = "/" + ShellIndex.FileName;
    private const string HtmlContentType = "text/html; charset=utf-8";
    private const string NoStore = "no-store";

    /// <summary>
    /// The shell's static files, except the index: served raw, it would lack the marker.
    /// The index is excluded whatever its case, as the file system may ignore it.
    /// </summary>
    public static void UseShellFiles(this WebApplication application)
    {
        var shellDirectory = application.Services.GetRequiredService<DesktopOptions>()
            .ShellDirectory;
        if (!Directory.Exists(shellDirectory))
        {
            // The smoke check reports the missing shell; the host still starts.
            return;
        }

        var files = new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.GetFullPath(shellDirectory)),
        };
        application.UseWhen(
            context => !context.Request.Path.Equals(IndexPath, StringComparison.OrdinalIgnoreCase),
            branch => branch.UseStaticFiles(files));
    }

    public static void MapShell(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(DesktopRoutes.Health, static () => TypedResults.NoContent());
        endpoints.MapGet("/", ServeIndex);
        endpoints.MapGet(IndexPath, ServeIndex);

        // Deep links of the Angular router (/fr/champions/…) load the index; unknown local
        // or API paths stay 404, never an HTML page that a fetch would misread.
        endpoints.MapFallback(static (HttpContext context, ShellIndex index) =>
            IsShellRoute(context.Request)
                ? ServeIndex(index, context.Response)
                : TypedResults.NotFound());
    }

    private static bool IsShellRoute(HttpRequest request) =>
        HttpMethods.IsGet(request.Method)
        && !request.Path.StartsWithSegments(DesktopRoutes.Api, StringComparison.OrdinalIgnoreCase)
        && !request.Path.StartsWithSegments(
            DesktopRoutes.Desktop,
            StringComparison.OrdinalIgnoreCase);

    private static IResult ServeIndex(ShellIndex index, HttpResponse response)
    {
        var html = index.Render();
        if (html is null)
        {
            return TypedResults.NotFound();
        }

        // The marker changes with each launch (bridge, port): the index is never cached.
        response.Headers[HeaderNames.CacheControl] = NoStore;
        return TypedResults.Content(html, HtmlContentType);
    }
}
