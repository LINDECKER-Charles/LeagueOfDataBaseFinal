using System.Net;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.States;
using LoDb.Ingestion.Pipeline.Versions;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Seo.IndexNow;

/// <summary>
/// Submits to IndexNow, at the promotion of a patch, the pages it touched in every locale
/// (<see cref="IndexNowPlan"/>): the short URLs, the pages of changes and the openers of the
/// states it closed.
/// </summary>
/// <remarks>
/// <para>
/// Runs last among the promotion listeners, once the index of states covers the version: what
/// changed is read there. An index that does not cover it yet is logged and nothing is
/// submitted, never a list computed on a stale index.
/// </para>
/// <para>
/// Off without <see cref="SeoOptions.CanonicalOrigin"/>. With the origin but without
/// <see cref="SeoOptions.IndexNowKey"/>, as on staging, one line tells what would be sent, and
/// nothing is. One summary line per promotion. A refusal ends the submission, and an
/// unreachable endpoint is logged by the ingestion (<c>ingest.listener.failed</c>): neither is
/// retried, since the sitemaps, dated by the same promotion, tell the engines all the same.
/// </para>
/// </remarks>
internal sealed partial class IndexNowSubmitter(
    CrawlerCatalog catalog,
    IEntityStateIndex states,
    IndexNowClient client,
    IOptions<SeoOptions> options,
    ILogger<IndexNowSubmitter> logger) : IPromotionListener
{
    public PromotionStage Stage => PromotionStage.Announce;

    public async Task OnPromotedAsync(PatchVersion version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        var (origin, key) = (options.Value.CanonicalOrigin, options.Value.IndexNowKey);
        if (string.IsNullOrEmpty(origin)
            || await PlanAsync(SeoOptionsValidator.Normalize(origin), version, cancellationToken)
                is not { } plan)
        {
            return;
        }

        if (string.IsNullOrEmpty(key))
        {
            LogPlanned(version, plan);
            return;
        }

        var target = new Target(SeoOptionsValidator.Normalize(origin), key);
        var submissions = plan.Urls.Chunk(IndexNowClient.MaxUrls)
            .Select(target.Submission)
            .ToList();
        await SubmitAsync(version, submissions, cancellationToken);
    }

    // What to announce, or null, logged, when the index or the catalog is not ready for it.
    private async Task<IndexNowPlan?> PlanAsync(
        string origin,
        PatchVersion version,
        CancellationToken cancellationToken)
    {
        var index = await states.GetAsync(cancellationToken);
        if (!index.Coverage.Contains(version))
        {
            LogNotIndexed(logger, version.Value);
            return null;
        }

        var load = await catalog.OpenAsync(version, cancellationToken);
        if (load is null || !load.IsReady)
        {
            LogSkipped(logger, version.Value);
            return null;
        }

        var promotion = new PromotionIndex(index, index.PromotedBefore(version));
        return IndexNowPlan.Of(origin, load.Catalog, promotion);
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

    private void LogPlanned(PatchVersion version, IndexNowPlan plan) =>
        LogPlanned(
            logger,
            version.Value,
            plan.UrlCount,
            plan.RequestCount,
            plan.ShortUrls.Count,
            plan.PatchPages.Count,
            plan.Openers.Count);

    [LoggerMessage(
        EventName = "seo.indexnow.planned",
        Level = LogLevel.Information,
        Message = "No IndexNow key: {Urls} URLs of {Version} in {Requests} requests would be"
            + " submitted ({ShortUrls} short URLs, {PatchPages} pages of changes, {Openers}"
            + " openers); nothing is sent.")]
    private static partial void LogPlanned(
        ILogger logger,
        string version,
        int urls,
        int requests,
        int shortUrls,
        int patchPages,
        int openers);

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

    [LoggerMessage(
        EventName = "seo.indexnow.unindexed",
        Level = LogLevel.Warning,
        Message = "The index of states does not cover {Version}: nothing is submitted to"
            + " IndexNow.")]
    private static partial void LogNotIndexed(ILogger logger, string version);

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
