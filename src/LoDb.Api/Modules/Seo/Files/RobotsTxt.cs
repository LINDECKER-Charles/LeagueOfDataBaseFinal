using System.Text;
using LoDb.Api.Modules.Seo.Sitemaps;

namespace LoDb.Api.Modules.Seo.Files;

/// <summary>The body of <c>/robots.txt</c>.</summary>
/// <remarks>
/// <para>
/// A single group on purpose: robots.txt groups are not inherited, so a block per agent
/// (GPTBot, ClaudeBot, PerplexityBot…) would make that agent ignore this one. Answer engines
/// are welcome: being readable by them is the point of <c>/llms.txt</c>.
/// </para>
/// <para>
/// No <c>Crawl-delay</c>, and no <c>Disallow</c> of the past patches, whose pages stay
/// indexable. Not every page is cheap: the proxy keeps the latest patch's pages, but a past
/// patch's page is rendered on request, from a catalog that may have to be loaded first. That
/// cost is bounded where it is paid, not here: the server-side renderer admits the renders of
/// past patches within a budget and answers 503 with <c>Retry-After</c> beyond it, which
/// well-behaved crawlers honour, and the sitemap index only announces the recent patches. A
/// delay would not help: Google ignores it, and it would hold the others to a full crawl
/// slower than patches ship. <c>/b/</c> stays crawlable so that crawlers read its
/// <c>noindex</c>.
/// </para>
/// </remarks>
internal static class RobotsTxt
{
    public const string ContentType = "text/plain; charset=utf-8";

    private static readonly string[] Disallowed =
    [
        "/admin",
        "/api/",
        "/v1/",
        "/webhooks/",
        "/*/account/",
        "/*/donate/checkout",
        "/*/donate/success",
        "/*/donate/cancel",
    ];

    /// <param name="origin">Canonical origin, no trailing slash.</param>
    public static string Build(string origin)
    {
        var body = new StringBuilder().Append("User-agent: *\n");
        foreach (var path in Disallowed)
        {
            body.Append("Disallow: ").Append(path).Append('\n');
        }

        return body
            .Append("Allow: /\n\n")
            .Append("Sitemap: ").Append(origin).Append(SitemapFile.IndexPath).Append('\n')
            .ToString();
    }
}
