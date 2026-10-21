using System.Globalization;
using LoDb.Api.Modules.Admin.Prerender.Demand.Queue;
using LoDb.Api.Modules.Admin.Prerender.Demand.Telemetry;
using LoDb.Api.Modules.Admin.Prerender.Demand.Validation;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Passes;
using LoDb.Api.Modules.Admin.Prerender.Telemetry;
using LoDb.Api.Modules.Admin.Prerender.Units;
using LoDb.Api.Modules.Admin.Prerender.Writing;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Drain;

/// <summary>
/// Writes one page of the queue into the served epoch, unless it should not be now: its
/// version is not an older patch for every server yet, its catalogue is not stored, or a pass
/// stored it meanwhile. Then saves how it ended.
/// </summary>
/// <remarks>
/// A refusal of the SSR is waited out as its Retry-After asks, <see cref="MostRefusals"/>
/// times at most; a pace that holds the renders, <see cref="MostHolds"/> times: the page then
/// waits for a later batch. A failure that may not last counts a try and leaves the page for a
/// later batch, failed for good at <see cref="DemandOptions.MaxAttempts"/>.
/// </remarks>
internal sealed class DemandPageRunner(
    IPrerenderCatalog catalog,
    PageIndexCache indexes,
    DemandQueue queue,
    DemandPageWriter writer,
    DemandMetrics metrics,
    IOptions<DemandOptions> options,
    TimeProvider clock)
{
    /// <summary>The refusals a page waits out in one batch.</summary>
    public const int MostRefusals = 10;

    /// <summary>The holds of the pace a page waits out in one batch, 10 s each.</summary>
    public const int MostHolds = 30;

    // No pass row: the key of a page of the queue only needs the epoch.
    private const long NoPass = 0;
    private const string NotPage = "Not a page of the catalogue.";
    private const string NotListed = "The catalogue of its version no longer lists it.";
    private const string NotPinned = "Its version is not an older patch.";
    private const string NotStored = "Its catalogue is not stored yet.";
    private const string TooManyRefusals = "The SSR refused it again and again.";
    private const string HeldBack = "The pace held the renders back.";

    /// <summary>How the page ended; null when another worker took it first.</summary>
    public async Task<DemandEnd?> RunAsync(
        DemandRow row,
        int epoch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        var page = DemandPath.Parse(row.Path);
        var end = page is null
            ? DemandEnd.Skipped(NotPage)
            : await WhyNotAsync(page, epoch, cancellationToken);
        if (end is null)
        {
            if (!await queue.ClaimAsync(row.Path, cancellationToken))
            {
                return null;
            }

            end = await RenderAsync(JobOf(page!, epoch), row.Attempts, cancellationToken);
        }

        await queue.EndAsync(row.Path, end, epoch, cancellationToken);
        Count(end);
        return end;
    }

    private async Task<DemandEnd?> WhyNotAsync(
        DemandPath page,
        int epoch,
        CancellationToken cancellationToken)
    {
        var from = await catalog.PinnedFromAsync(page.Version, cancellationToken);
        if (from is null)
        {
            return DemandEnd.Skipped(NotPinned);
        }

        if (from > clock.GetUtcNow())
        {
            return DemandEnd.Deferred(SettlingUntil(from.Value));
        }

        var index = await indexes.OfAsync(page.Version, cancellationToken);
        return index is null ? DemandEnd.Deferred(NotStored)
            : !index.Lists(page.PagePath) ? DemandEnd.Skipped(NotListed)
            : await queue.IsStoredAsync(page, index, epoch, cancellationToken)
                ? DemandEnd.Skipped(ServedUnits.AlreadyStored)
            : null;
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
                var isLast = attempts + 1 >= options.Value.MaxAttempts;
                return DemandEnd.Failed(reason, isLast) with { Refusals = refusals };
            }

            if (attempt.IsHeld ? ++holds >= MostHolds : ++refusals >= MostRefusals)
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

    private static PageJob JobOf(DemandPath page, int epoch) => new(
        new ClaimedUnit
        {
            Pass = new RunningPass(NoPass, epoch, PassScope.Everything),
            Version = page.Version,
            Locale = page.Locale,
        },
        page.PagePath);

    private static string SettlingUntil(DateTimeOffset from) =>
        "Its version just left the latest rank; the servers hold it for the latest patch until "
        + from.ToString("O", CultureInfo.InvariantCulture) + ".";
}
