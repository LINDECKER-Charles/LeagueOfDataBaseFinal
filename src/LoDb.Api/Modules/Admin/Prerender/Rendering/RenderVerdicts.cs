using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using HeaderNames = Microsoft.Net.Http.Headers.HeaderNames;

namespace LoDb.Api.Modules.Admin.Prerender.Rendering;

/// <summary>
/// Reads what the SSR answered for a page into what the pass does with it: only a 200 the SSR
/// marked with a stored class (<c>archived</c> or <c>revisable</c>), compressed in gzip and
/// named after its build, is ever written.
/// </summary>
/// <remarks>
/// The class comes from the SSR's mark, never from a copy of its <c>Cache-Control</c>, which
/// would drift from the front's constant; the object keeps that <c>Cache-Control</c>, which
/// nginx follows. A mark the pass does not know is skipped, as an unmarked page. A 200 kept
/// out of every cache (<c>no-store</c>) is a render that missed part of itself, such as a
/// catalog that failed to load: tried again, never skipped for good.
/// </remarks>
internal static partial class RenderVerdicts
{
    public static RenderVerdict Of(RenderedPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var status = (int)page.Status;
        return page.Status switch
        {
            HttpStatusCode.OK => OfSuccess(page),
            HttpStatusCode.ServiceUnavailable or HttpStatusCode.TooManyRequests =>
                RenderVerdict.Refused,
            HttpStatusCode.NotFound or HttpStatusCode.Gone => RenderVerdict.Skipped,
            _ when status is >= 300 and < 400 => RenderVerdict.Skipped,
            _ when status is >= 400 and < 500 => RenderVerdict.Misconfigured,
            _ => RenderVerdict.Transient,
        };
    }

    private static RenderVerdict OfSuccess(RenderedPage page)
    {
        var mark = page.Header(PrerenderHeaders.CacheClass);
        if (mark is null || !PrerenderHeaders.StoredClasses.Contains(mark))
        {
            return IsKeptOutOfCaches(page) ? RenderVerdict.Transient : RenderVerdict.Skipped;
        }

        var encoding = page.Header(HeaderNames.ContentEncoding)?.Trim();
        var isGzip = string.Equals(encoding, PrerenderHeaders.Gzip, StringComparison.Ordinal);
        var build = page.Header(PrerenderHeaders.Build);
        return isGzip && build is not null && BuildDigest().IsMatch(build)
            ? RenderVerdict.Storable
            : RenderVerdict.Transient;
    }

    private static bool IsKeptOutOfCaches(RenderedPage page) =>
        CacheControlHeaderValue.TryParse(page.Header(HeaderNames.CacheControl), out var value)
        && value is { NoStore: true };

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex BuildDigest();
}
