using System.Globalization;
using LoDb.Api.Modules.Admin.Prerender.Demand.Drain;
using LoDb.Api.Modules.Admin.Prerender.Demand.Queue;
using LoDb.Api.Modules.Admin.Prerender.Demand.Validation;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Passes;
using LoDb.Api.Modules.Admin.Prerender.Revision.Queue;
using LoDb.Api.Modules.Admin.Prerender.Revision.Telemetry;
using LoDb.Api.Modules.Admin.Prerender.Telemetry;
using LoDb.Api.Modules.Admin.Prerender.Units;
using LoDb.Api.Modules.Admin.Prerender.Writing;
using LoDb.Infrastructure.Persistence.Prerender.Demand;

namespace LoDb.Api.Modules.Admin.Prerender.Revision.Drain;

/// <summary>
/// Writes one page of a closed state again, into the epoch of its row, by the automatic
/// queue's path (<see cref="DemandPageWriter"/>: the build copied first, the same verdicts, the
/// same key and metadata), unless it should not be now: its epoch is retired, or its version is
/// not an older patch for every server yet. Then saves how it ended.
/// </summary>
/// <remarks>
/// Its stored copy is the one to replace: a pass that wrote it is no reason to skip it, unlike
/// the automatic queue. The row names the page as the state's opener spells it; the page
/// rendered is the one the catalogue of its version lists (<see cref="VersionPageIndex"/>), the
/// copy a pass stored. A refusal of the SSR is waited out as its Retry-After asks,
/// <see cref="DemandPageRunner.MostRefusals"/> times at most; a pace that holds the renders,
/// <see cref="DemandPageRunner.MostHolds"/> times: the page is then due again later. A failure
/// that may not last counts a try, failed for good at <see cref="MaxAttempts"/>.
/// </remarks>
internal sealed class RevisionPageRunner(
    IPrerenderCatalog catalog,
    PageIndexCache indexes,
    RevisionQueue queue,
    DemandPageWriter writer,
    RevisionMetrics metrics,
    TimeProvider clock)
{
    /// <summary>Failed tries before a page is failed for good.</summary>
    public const int MaxAttempts = 3;

    // No pass row: the key of a page of the queue only needs the epoch.
    private const long NoPass = 0;
    private const string NotPage = "Not a page of the catalogue.";
    private const string Retired =
        "Its epoch is older than the served one: nginx no longer reads it.";
    private const string NotPinned = "Its version is not an older patch.";
    private const string TooManyRefusals = "The SSR refused it again and again.";
    private const string HeldBack = "The pace held the renders back.";

    /// <summary>How the page ended; null when another worker took it first.</summary>
    public async Task<DemandEnd?> RunAsync(
        RevisionRow row,
        int servedEpoch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        var page = DemandPath.Parse(row.Path);
        var end = page is null ? DemandEnd.Skipped(NotPage)
            : row.Epoch < servedEpoch ? DemandEnd.Skipped(Retired)
            : await WhyNotAsync(page, cancellationToken);
        if (end is null)
        {
            var job = JobOf(page!, await SpellingAsync(page!, cancellationToken), row.Epoch);
            if (!await queue.ClaimAsync(row, cancellationToken))
            {
                return null;
            }

            end = await RenderAsync(job, row.Attempts, cancellationToken);
        }

        await queue.EndAsync(row, end, cancellationToken);
        Count(end);
        return end;
    }

    private async Task<DemandEnd?> WhyNotAsync(
        DemandPath page,
        CancellationToken cancellationToken)
    {
        var from = await catalog.PinnedFromAsync(page.Version, cancellationToken);
        return from is null ? DemandEnd.Skipped(NotPinned)
            : from > clock.GetUtcNow() ? DemandEnd.Deferred(SettlingUntil(from.Value))
            : null;
    }

    // The page as its version spells it, or as queued when its catalogue cannot say.
    private async Task<string> SpellingAsync(
        DemandPath page,
        CancellationToken cancellationToken)
    {
        var listed = await indexes.OfAsync(page.Version, cancellationToken);
        return listed?.SpellingOf(page.PagePath) ?? page.PagePath;
    }

    private async Task<DemandEnd> RenderAsync(
        PageJob job,
        int attempts,
        CancellationToken cancellationToken)
    {
        var (refusals, holds) = (0, 0);
        while (true)
        {
            var attempt = await writer.TryAsync(job, cancellationToken);
            if (attempt.End is { } end)
            {
                return end with { Refusals = refusals };
            }

            if (attempt.Failure is { } reason)
            {
                var isLast = attempts + 1 >= MaxAttempts;
                return DemandEnd.Failed(reason, isLast) with { Refusals = refusals };
            }

            if (attempt.IsHeld
                ? ++holds >= DemandPageRunner.MostHolds
                : ++refusals >= DemandPageRunner.MostRefusals)
            {
                var why = attempt.IsHeld ? HeldBack : TooManyRefusals;
                return DemandEnd.Deferred(why) with { Refusals = refusals };
            }

            await WaitOutAsync(attempt, cancellationToken);
        }
    }

    private async Task WaitOutAsync(DemandTry attempt, CancellationToken cancellationToken)
    {
        if (!attempt.IsHeld)
        {
            metrics.Count(PageOutcome.Refused);
            await Task.Delay(attempt.Wait, clock, cancellationToken);
        }
    }

    // A page left for later is no outcome: it ends in a later batch.
    private void Count(DemandEnd end)
    {
        PageOutcome? outcome = end.State switch
        {
            PrerenderDemandState.Done => PageOutcome.Written,
            PrerenderDemandState.Skipped => PageOutcome.Skipped,
            _ when end.IsFailedTry => PageOutcome.Failed,
            _ => null,
        };
        if (outcome is { } ended)
        {
            metrics.Count(ended);
        }
    }

    private static PageJob JobOf(DemandPath page, string pagePath, int epoch) => new(
        new ClaimedUnit
        {
            Pass = new RunningPass(NoPass, epoch, PassScope.Everything),
            Version = page.Version,
            Locale = page.Locale,
        },
        pagePath);

    private static string SettlingUntil(DateTimeOffset from) =>
        "Its version just left the latest rank; the servers hold it for the latest patch until "
        + from.ToString("O", CultureInfo.InvariantCulture) + ".";
}
