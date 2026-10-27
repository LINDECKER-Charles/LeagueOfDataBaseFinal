using LoDb.Api.Modules.Admin.Prerender.Rendering;
using LoDb.Infrastructure.PageStore;
using HeaderNames = Microsoft.Net.Http.Headers.HeaderNames;

namespace LoDb.Api.Modules.Admin.Prerender.Writing;

/// <summary>
/// A rendered page as the store keeps it: its body as the SSR sent it, gzip; its type,
/// encoding and caching as object headers; the headers proper to the page as metadata, which
/// nginx sets again when it serves it (ADR 0011).
/// </summary>
/// <remarks>
/// <para>
/// The caching is the page's own <c>Cache-Control</c>, that of its class (a year for a closed
/// state's page, an hour for the open state's): nginx reads the object at every request and
/// gives the client the class it names (ADR 0011, amendment of 2026-10-27).
/// </para>
/// <para>
/// Neither <c>Vary</c> nor <c>Content-Length</c> is kept: nginx answers one copy whatever the
/// client accepts, and the store knows the length. The other headers are nginx's own.
/// </para>
/// </remarks>
internal static class PageObjects
{
    /// <summary>The metadata naming the build that rendered the page.</summary>
    public const string BuildMetadata = "lodb-build";

    private const string ReportingEndpoints = "Reporting-Endpoints";

    // Only an HTML page is ever marked with a stored class: the type it would have, were it not
    // sent.
    private const string Html = "text/html; charset=utf-8";

    // The page's own headers, which depend on its scripts (CSP) or its images (preconnect).
    private static readonly string[] PageHeaders =
    [
        HeaderNames.ContentSecurityPolicy,
        HeaderNames.ContentSecurityPolicyReportOnly,
        ReportingEndpoints,
        HeaderNames.Link,
    ];

    /// <summary>
    /// The object of <paramref name="page"/>, a <c>Storable</c> render, at its key.
    /// </summary>
    public static PageObject Of(PageJob job, RenderedPage page)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(page);
        return new PageObject
        {
            Key = job.Key,
            Body = page.Body,
            ContentType = page.Header(HeaderNames.ContentType) ?? Html,
            ContentEncoding = page.Header(HeaderNames.ContentEncoding),
            CacheControl = page.Header(HeaderNames.CacheControl),
            StorageClass = PageStorageClass.OneZone,
            Metadata = MetadataOf(page),
        };
    }

    private static Dictionary<string, string> MetadataOf(RenderedPage page)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in PageHeaders)
        {
            if (page.Header(name) is { } value)
            {
                metadata[name.ToLowerInvariant()] = value;
            }
        }

        if (page.Header(PrerenderHeaders.Build) is { } build)
        {
            metadata[BuildMetadata] = build;
        }

        return metadata;
    }
}
