using System.Globalization;
using System.Text;

namespace LoDb.Api.Modules.Seo;

/// <summary>
/// An answer of the crawler-facing files: a document, a permanent redirect, a missing file
/// or a file that is not ready yet, each with its own caching.
/// </summary>
/// <remarks>
/// Redirects and errors are kept a minute by shared caches: the latest version, and hence
/// where a sitemap redirects, changes with every patch. A 503 is never stored, and names
/// the delay after which a crawler may ask again.
/// </remarks>
internal sealed record SeoAnswer : IResult
{
    /// <summary>Delay a crawler waits for a sitemap whose datasets are queued.</summary>
    public static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

    private const string ShortCache = "public, max-age=0, s-maxage=60";
    private const string NoStore = "no-store";
    private const string PlainText = "text/plain; charset=utf-8";

    private SeoAnswer()
    {
    }

    public int Status { get; private init; }

    public string CacheControl { get; private init; } = NoStore;

    public string? ContentType { get; private init; }

    public byte[] Body { get; private init; } = [];

    public string? Location { get; private init; }

    public TimeSpan? RetryAfter { get; private init; }

    /// <summary>A 200 document.</summary>
    public static SeoAnswer File(byte[] body, string contentType, string cacheControl) => new()
    {
        Status = StatusCodes.Status200OK,
        Body = body,
        ContentType = contentType,
        CacheControl = cacheControl,
    };

    /// <summary>A 200 text document, encoded in UTF-8.</summary>
    public static SeoAnswer Text(string body, string contentType, string cacheControl) =>
        File(Encoding.UTF8.GetBytes(body), contentType, cacheControl);

    /// <summary>A 301 to a path of the site.</summary>
    public static SeoAnswer MovedTo(string path) => new()
    {
        Status = StatusCodes.Status301MovedPermanently,
        Location = path,
        CacheControl = ShortCache,
    };

    public static SeoAnswer Missing() => new()
    {
        Status = StatusCodes.Status404NotFound,
        Body = Encoding.UTF8.GetBytes("Not Found\n"),
        ContentType = PlainText,
        CacheControl = ShortCache,
    };

    /// <summary>A 503: the datasets are queued, or Data Dragon failed.</summary>
    public static SeoAnswer Unavailable() => new()
    {
        Status = StatusCodes.Status503ServiceUnavailable,
        Body = Encoding.UTF8.GetBytes("Service Unavailable\n"),
        ContentType = PlainText,
        RetryAfter = RetryDelay,
    };

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var response = httpContext.Response;
        response.StatusCode = Status;
        response.Headers.CacheControl = CacheControl;
        if (Location is not null)
        {
            response.Headers.Location = Location;
        }

        if (RetryAfter is { } delay)
        {
            response.Headers.RetryAfter =
                ((long)delay.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        if (ContentType is not null)
        {
            response.ContentType = ContentType;
            response.ContentLength = Body.Length;
            await response.Body.WriteAsync(Body, httpContext.RequestAborted);
        }
    }
}
