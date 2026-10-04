namespace LoDb.Api.Modules.Seo.Files;

/// <summary>
/// Answers <c>/robots.txt</c> and <c>/llms.txt</c>, built with the canonical origin.
/// </summary>
internal sealed class SeoFilesPublisher(CrawlerCatalog catalog, SiteOrigin origins)
{
    public SeoAnswer Robots(HttpRequest request)
    {
        var body = RobotsTxt.Build(origins.Of(request));
        return SeoAnswer.Text(body, RobotsTxt.ContentType, SeoCaching.Hour);
    }

    /// <summary>
    /// The brief of the latest version. It never fails: without a catalog it drops the
    /// counts, as a partial brief would state wrong figures.
    /// </summary>
    public async Task<SeoAnswer> LlmsAsync(HttpRequest request)
    {
        var inventory = await ReadInventoryAsync(request.HttpContext.RequestAborted);
        var body = LlmsTxt.Build(origins.Of(request), inventory);
        return SeoAnswer.Text(body, LlmsTxt.ContentType, SeoCaching.Hour);
    }

    private async Task<SiteInventory> ReadInventoryAsync(CancellationToken cancellationToken)
    {
        var latest = await catalog.GetLatestAsync(cancellationToken);
        if (latest is null)
        {
            return SiteInventory.Empty;
        }

        var load = await catalog.OpenAsync(latest, cancellationToken);
        return new SiteInventory(latest, load?.IsReady == true ? load.Catalog : null);
    }
}
