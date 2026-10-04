using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Reading;

namespace LoDb.Api.Modules.Seo.Sitemaps;

/// <summary>
/// Answers every path below <c>/sitemaps/</c> and <c>/sitemap.xml</c>, the forms of the
/// previous site included.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><c>/sitemap.xml</c>: the index of every locale's sitemaps.</item>
/// <item><c>/sitemaps/{locale}/latest.xml</c>: unversioned pages and short entity URLs.</item>
/// <item><c>/sitemaps/{locale}/{version}.xml</c>: one past version; the latest one moves
/// to <c>latest.xml</c>.</item>
/// <item>Previous forms: <c>/sitemaps/latest.xml</c> moves to the index,
/// <c>/sitemaps/{version}.xml</c> to its <c>en</c> sitemap, in a single hop.</item>
/// </list>
/// </remarks>
internal sealed class SitemapPublisher(CrawlerCatalog catalog, SiteOrigin origins)
{
    public async Task<SeoAnswer> IndexAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var versions = await catalog.GetVersionsAsync(cancellationToken);
        if (versions is null)
        {
            return SeoAnswer.Unavailable();
        }

        var body = SitemapXml.Index(SitemapLocations.Index(origins.Of(request), versions));
        return SeoAnswer.File(body, SitemapXml.ContentType, SeoCaching.Hour);
    }

    /// <summary>A file of the previous site, moved to its locale-prefixed form.</summary>
    public async Task<SeoAnswer> PreviousAsync(string file, CancellationToken cancellationToken)
    {
        if (!SitemapFile.TryRead(file, out var sitemap))
        {
            return SeoAnswer.Missing();
        }

        if (sitemap.IsLatest)
        {
            return SeoAnswer.MovedTo(SitemapFile.IndexPath);
        }

        var latest = await catalog.GetLatestAsync(cancellationToken);
        var version = sitemap.Version == latest ? null : sitemap.Version;
        return SeoAnswer.MovedTo(SitemapFile.PathOf(UiLocales.Fallback, version));
    }

    /// <summary>A locale's sitemap; the request's abort token cancels the reads.</summary>
    public async Task<SeoAnswer> LocaleAsync(HttpRequest request, string locale, string file)
    {
        var cancellationToken = request.HttpContext.RequestAborted;
        if (!UiLocales.TryParse(locale, out var uiLocale)
            || !SitemapFile.TryRead(file, out var sitemap))
        {
            return SeoAnswer.Missing();
        }

        var latest = await catalog.GetLatestAsync(cancellationToken);
        var scope = new SitemapScope(origins.Of(request), uiLocale, latest);
        if (sitemap.Version is not { } version)
        {
            return await LatestAsync(scope, cancellationToken);
        }

        return version == latest
            ? SeoAnswer.MovedTo(SitemapFile.PathOf(uiLocale, null))
            : await VersionAsync(scope, version, cancellationToken);
    }

    private async Task<SeoAnswer> LatestAsync(
        SitemapScope scope,
        CancellationToken cancellationToken)
    {
        CatalogLoad? load = null;
        if (scope.Latest is { } latest)
        {
            load = await catalog.OpenAsync(latest, cancellationToken);
            if (load?.IsReady != true)
            {
                return SeoAnswer.Unavailable();
            }
        }

        var pages = SitemapLocations.Latest(scope.Origin, scope.Locale, load?.Catalog);
        return SeoAnswer.File(SitemapXml.UrlSet(pages), SitemapXml.ContentType, SeoCaching.Hour);
    }

    private async Task<SeoAnswer> VersionAsync(
        SitemapScope scope,
        PatchVersion version,
        CancellationToken cancellationToken)
    {
        var load = await catalog.OpenAsync(version, cancellationToken);
        if (load?.Status == CatalogLoadStatus.Unknown)
        {
            return SeoAnswer.Missing();
        }

        if (load is null || !load.IsReady)
        {
            return SeoAnswer.Unavailable();
        }

        // A version newer than the latest one is promoted later: its sitemap then moves.
        var cache = version < scope.Latest ? SeoCaching.Immutable : SeoCaching.Hour;
        var pages = SitemapLocations.Version(scope.Origin, scope.Locale, load.Catalog);
        return SeoAnswer.File(SitemapXml.UrlSet(pages), SitemapXml.ContentType, cache);
    }

    private sealed record SitemapScope(string Origin, UiLocale Locale, PatchVersion? Latest);
}
