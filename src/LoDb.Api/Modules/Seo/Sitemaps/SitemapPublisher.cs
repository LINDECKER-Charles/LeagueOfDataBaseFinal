using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Catalog.States;
using LoDb.Ingestion.Catalog.States.Snapshots;

namespace LoDb.Api.Modules.Seo.Sitemaps;

/// <summary>
/// Answers every path below <c>/sitemaps/</c> and <c>/sitemap.xml</c>, the forms of the
/// previous site included.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><c>/sitemap.xml</c>: every locale's <c>latest.xml</c>, then the files of the covered
/// past versions that list something, read in the index of states.</item>
/// <item><c>/sitemaps/{locale}/latest.xml</c>: unversioned pages and short entity URLs, read in
/// the latest catalog.</item>
/// <item><c>/sitemaps/{locale}/{version}.xml</c>: the indexable pages of a covered past
/// version, read in the index of states alone, empty or not; the latest version moves to
/// <c>latest.xml</c>, a newer or uncovered one is missing.</item>
/// <item>Previous forms: <c>/sitemaps/latest.xml</c> moves to the index,
/// <c>/sitemaps/{version}.xml</c> to its <c>en</c> sitemap, in a single hop.</item>
/// </list>
/// A 503 only while the index of states has no copy loaded, or the latest catalog is queued.
/// </remarks>
internal sealed class SitemapPublisher(
    CrawlerCatalog catalog,
    IEntityStateIndex states,
    SiteOrigin origins)
{
    public async Task<SeoAnswer> IndexAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (await ReadIndexAsync(cancellationToken) is not { } index)
        {
            return SeoAnswer.Unavailable();
        }

        var latest = await LatestOfAsync(index, cancellationToken);
        var locations = SitemapLocations.Index(origins.Of(request), latest, index);
        return Document(SitemapXml.Index(locations));
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
        if (sitemap.Version is { } version && NotPast(uiLocale, version, latest) is { } answer)
        {
            return answer;
        }

        if (await ReadIndexAsync(cancellationToken) is not { } index)
        {
            return SeoAnswer.Unavailable();
        }

        var prefix = SitePages.Prefix(origins.Of(request), uiLocale, sitemap.Version);
        return sitemap.Version is { } past
            ? VersionFile(prefix, past, index)
            : await LatestFileAsync(prefix, index, cancellationToken);
    }

    // The latest version moves to latest.xml; a newer one, or any before the first promotion,
    // has no file. Null for a past version.
    private static SeoAnswer? NotPast(UiLocale locale, PatchVersion version, PatchVersion? latest)
    {
        if (version == latest)
        {
            return SeoAnswer.MovedTo(SitemapFile.PathOf(locale, null));
        }

        return latest is null || version > latest ? SeoAnswer.Missing() : null;
    }

    private static SeoAnswer VersionFile(
        string prefix,
        PatchVersion version,
        EntityStateSnapshot index) =>
        index.Coverage.Contains(version)
            ? Document(SitemapXml.UrlSet(SitemapLocations.Version(prefix, version, index)))
            : SeoAnswer.Missing();

    private async Task<SeoAnswer> LatestFileAsync(
        string prefix,
        EntityStateSnapshot index,
        CancellationToken cancellationToken)
    {
        var latest = await LatestOfAsync(index, cancellationToken);
        CatalogLoad? load = null;
        if (latest.Version is { } version)
        {
            load = await catalog.OpenAsync(version, cancellationToken);
            if (load?.IsReady != true)
            {
                return SeoAnswer.Unavailable();
            }
        }

        return Document(SitemapXml.UrlSet(SitemapLocations.Latest(prefix, load?.Catalog, latest)));
    }

    private async Task<LatestPatch> LatestOfAsync(
        EntityStateSnapshot index,
        CancellationToken cancellationToken)
    {
        var version = await catalog.GetLatestAsync(cancellationToken);
        var promotedAt = await catalog.GetLatestPromotedAtAsync(cancellationToken);
        return LatestPatch.Of(version, promotedAt, index);
    }

    // The in-memory copy; null while none is loaded and the database cannot give one, which
    // the index already logs and counts.
    private async Task<EntityStateSnapshot?> ReadIndexAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await states.GetAsync(cancellationToken);
        }
        catch (StateIndexUnavailableException)
        {
            return null;
        }
    }

    private static SeoAnswer Document(byte[] body) =>
        SeoAnswer.File(body, SitemapXml.ContentType, SeoCaching.Hour);
}
