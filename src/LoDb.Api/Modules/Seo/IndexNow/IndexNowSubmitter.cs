using System.Net;
using LoDb.Api.Modules.Seo.Sitemaps;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Pipeline.Versions;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Seo.IndexNow;

/// <summary>
/// Submits to IndexNow, at the promotion of a patch, the pages it changed in every locale:
/// the short URLs the primary sitemaps date by that promotion.
/// </summary>
/// <remarks>
/// Off without <see cref="SeoOptions.IndexNowKey"/> or <see cref="SeoOptions.CanonicalOrigin"/>.
/// One summary line per promotion. A refusal ends the submission, and an unreachable
/// endpoint is logged by the ingestion (<c>ingest.listener.failed</c>): neither is retried,
/// since the sitemaps, dated by the same promotion, tell the engines all the same.
/// </remarks>
internal sealed partial class IndexNowSubmitter(
    CrawlerCatalog catalog,
    IndexNowClient client,
    IOptions<SeoOptions> options,
    ILogger<IndexNowSubmitter> logger) : IPromotionListener
{
    public async Task OnPromotedAsync(PatchVersion version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (TargetOf(options.Value) is not { } target)
        {
            return;
        }

        var load = await catalog.OpenAsync(version, cancellationToken);
        if (load?.IsReady != true)
        {
            LogSkipped(logger, version.Value);
            return;
        }

        var submissions = UiLocales.All
            .SelectMany(locale => SitemapLocations.PatchBound(
                SitePages.Prefix(target.Origin, locale, null),
                load.Catalog))
            .Chunk(IndexNowClient.MaxUrls)
            .Select(target.Submission)
            .ToList();
        await SubmitAsync(version, submissions, cancellationToken);
    }

    private static Target? TargetOf(SeoOptions settings)
    {
        var (origin, key) = (settings.CanonicalOrigin, settings.IndexNowKey);
        return string.IsNullOrEmpty(origin) || string.IsNullOrEmpty(key)
            ? null
            : new Target(SeoOptionsValidator.Normalize(origin), key);
    }

    private async Task SubmitAsync(
        PatchVersion version,
        List<IndexNowSubmission> submissions,
        CancellationToken cancellationToken)
    {
        var submitted = 0;
        foreach (var submission in submissions)
        {
            var status = await client.SubmitAsync(submission, cancellationToken);
            if (status is not (HttpStatusCode.OK or HttpStatusCode.Accepted))
            {
                LogRefused(logger, version.Value, (int)status, submitted);
                return;
            }

            submitted += submission.UrlList.Count;
        }

        LogSubmitted(logger, version.Value, submitted, submissions.Count);
    }

    [LoggerMessage(
        EventName = "seo.indexnow.submitted",
        Level = LogLevel.Information,
        Message = "Submitted {Urls} URLs of {Version} to IndexNow in {Requests} requests.")]
    private static partial void LogSubmitted(
        ILogger logger,
        string version,
        int urls,
        int requests);

    [LoggerMessage(
        EventName = "seo.indexnow.refused",
        Level = LogLevel.Warning,
        Message = "IndexNow answered {Status} for {Version} after {Urls} URLs; the rest is"
            + " given up.")]
    private static partial void LogRefused(ILogger logger, string version, int status, int urls);

    [LoggerMessage(
        EventName = "seo.indexnow.skipped",
        Level = LogLevel.Warning,
        Message = "The catalog of {Version} is not loaded: nothing is submitted to IndexNow.")]
    private static partial void LogSkipped(ILogger logger, string version);

    // The canonical origin, without a trailing slash, and the key it publishes.
    private sealed record Target(string Origin, string Key)
    {
        public IndexNowSubmission Submission(string[] urls) => new()
        {
            Host = new Uri(Origin).Host,
            Key = Key,
            KeyLocation = Origin + IndexNowKeyFile.PathOf(Key),
            UrlList = urls,
        };
    }
}
