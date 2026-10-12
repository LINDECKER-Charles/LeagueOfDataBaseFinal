using LoDb.Api.Modules.Seo.IndexNow;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Seo.Files;

/// <summary>
/// Answers <c>/robots.txt</c> and <c>/llms.txt</c>, built with the canonical origin, and the
/// IndexNow key file.
/// </summary>
internal sealed class SeoFilesPublisher(
    CrawlerCatalog catalog,
    SiteOrigin origins,
    IOptions<SeoOptions> options)
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

    /// <summary>
    /// The key file when <paramref name="key"/> is the configured key, a 404 otherwise: any
    /// other name, or no key at all.
    /// </summary>
    public SeoAnswer IndexNowKey(string key)
    {
        var configured = options.Value.IndexNowKey;
        var isKey = !string.IsNullOrEmpty(configured)
            && string.Equals(key, configured, StringComparison.Ordinal);
        return isKey
            ? SeoAnswer.Text(configured!, IndexNowKeyFile.ContentType, SeoCaching.Hour)
            : SeoAnswer.Missing();
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
