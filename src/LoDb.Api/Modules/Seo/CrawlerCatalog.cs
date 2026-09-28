using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Egress.Errors;
using LoDb.Ingestion.Queue;

namespace LoDb.Api.Modules.Seo;

/// <summary>
/// The catalog reads of the sitemaps and of <c>llms.txt</c>, on behalf of a crawler.
/// </summary>
/// <remarks>
/// A sitemap lists the same pages in every locale: ids and canonical paths do not depend on
/// the language, so the en_US catalog answers for all 21. A crawler never waits for an
/// ingestion: a cold version's datasets are queued within the crawler budget (C5) and the
/// sitemap answers 503 until they are stored. <see langword="null"/> stands for a Data
/// Dragon failure.
/// </remarks>
internal sealed partial class CrawlerCatalog(
    ICatalogReader reader,
    ILogger<CrawlerCatalog> logger)
{
    private static readonly ColdDemand CrawlerDemand =
        ColdDemand.Queued.For(OnDemandOrigin.Crawler);

    public Task<PatchVersion?> GetLatestAsync(CancellationToken cancellationToken) =>
        reader.GetLatestAsync(cancellationToken);

    /// <summary>Every listed version and the latest one, or null when Data Dragon failed.</summary>
    public async Task<CatalogVersions?> GetVersionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await reader.GetVersionsAsync(cancellationToken);
        }
        catch (EgressException exception)
        {
            LogUpstreamFailed(logger, "versions", exception);
            return null;
        }
    }

    /// <summary>The en_US catalog of a version, or null when Data Dragon failed.</summary>
    public async Task<CatalogLoad?> OpenAsync(
        PatchVersion version,
        CancellationToken cancellationToken)
    {
        try
        {
            return await reader.GetAsync(
                version,
                DdragonLanguage.EnUs,
                CrawlerDemand,
                cancellationToken);
        }
        catch (EgressException exception)
        {
            LogUpstreamFailed(logger, version.Value, exception);
            return null;
        }
    }

    [LoggerMessage(
        EventName = "seo.catalog.failed",
        Level = LogLevel.Warning,
        Message = "Crawler read of {Scope} failed: Data Dragon failed.")]
    private static partial void LogUpstreamFailed(
        ILogger logger,
        string scope,
        Exception exception);
}
