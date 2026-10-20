using System.Globalization;
using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Passes;
using LoDb.Api.Modules.Admin.Prerender.Telemetry;
using LoDb.Api.Modules.Admin.Prerender.Writing;
using LoDb.Infrastructure.Persistence.Prerender;
using LoDb.Ingestion.Catalog.Reading;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Units;

/// <summary>
/// Writes the pages of one unit, from its cursor to its last page, then logs its summary: one
/// line per unit, never one per page.
/// </summary>
/// <remarks>
/// <para>
/// The catalog of the version is loaded once for all its pages, here and by the API the SSR
/// calls: the pass goes (version, locale) after (version, locale) for that reason. Within a
/// unit, <see cref="PrerenderOptions.MaxInFlight"/> pages are rendered at once
/// (<see cref="PageWindow"/>).
/// </para>
/// <para>
/// A unit waits while its catalog is not stored, and while some server may still hold its
/// version for the latest one: a page rendered then would come back as the latest patch's,
/// skipped for good. Past <see cref="UnitWaits.Longest"/>, the units of the version fail.
/// </para>
/// </remarks>
internal sealed class UnitRunner(
    IPrerenderCatalog catalog,
    UnitQueue queue,
    PageRunner pages,
    PassControl control,
    UnitWaits waits,
    IOptions<PrerenderOptions> options,
    TimeProvider clock,
    ILogger<UnitRunner> logger)
{
    private const string NoFailure = "none.";
    private const string NotStored = "its catalog is not stored yet.";
    private const string NotPinned = "the API does not count it among the older patches yet.";

    public async Task<UnitEnd> RunAsync(ClaimedUnit unit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unit);
        var list = await catalog.PagesAsync(unit.Version, cancellationToken);
        if (list.Status == CatalogLoadStatus.Unknown)
        {
            return await UnknownAsync(unit, cancellationToken);
        }

        if (await WhyWaitAsync(unit, list.Status, cancellationToken) is { } reason)
        {
            return await WaitAsync(unit, reason, cancellationToken);
        }

        waits.End(unit);
        var paths = PageTypes.Keep(list.Paths, unit.Pass.Scope.PageTypes);
        await queue.StartAsync(unit, paths.Count, cancellationToken);
        var walk = new UnitWalk(unit, paths, new UnitTally());
        var started = clock.GetTimestamp();
        var end = await WalkAsync(walk, cancellationToken);
        if (end == UnitEnd.Finished)
        {
            await queue.FinishAsync(unit, PrerenderUnitState.Completed, cancellationToken);
            LogCompleted(walk, (long)clock.GetElapsedTime(started).TotalMilliseconds);
        }

        return end;
    }

    private async Task<UnitEnd> WalkAsync(UnitWalk walk, CancellationToken cancellationToken)
    {
        var window = new PageWindow(walk.Unit.Cursor, walk.Paths.Count, options.Value.MaxInFlight);
        PageResult? stop;
        try
        {
            stop = await RunWindowAsync(walk, window, cancellationToken);
        }
        finally
        {
            await window.SettleAsync();
        }

        return stop is null
            ? UnitEnd.Finished
            : await StopAsync(walk.Unit, stop, cancellationToken);
    }

    // The pages of the window, saved as they end in a row; once a page stops (a pause, a
    // cancel, a setting that fails them all), no other starts and the ones in flight end. The
    // page that stopped, if one did.
    private async Task<PageResult?> RunWindowAsync(
        UnitWalk walk,
        PageWindow window,
        CancellationToken cancellationToken)
    {
        PageResult? stop = null;
        window.Fill(Start);
        while (!window.IsIdle)
        {
            var (index, result) = await window.NextEndedAsync();
            walk.Tally.Add(result);
            if (result.Outcome is null)
            {
                stop ??= result;
            }
            else if (window.End(index, result) is { } step)
            {
                await queue.RecordAsync(walk.Unit, step, cancellationToken);
            }

            if (stop is null)
            {
                window.Fill(Start);
            }
        }

        return stop;

        Task<PageResult> Start(int index) =>
            pages.RunAsync(new PageJob(walk.Unit, walk.Paths[index]), cancellationToken);
    }

    // The pass was paused or cancelled, or it cannot go on as configured: then it pauses, so
    // that no page is counted against a setting that fails them all.
    private async Task<UnitEnd> StopAsync(
        ClaimedUnit unit,
        PageResult result,
        CancellationToken cancellationToken)
    {
        if (result.IsMisconfigured)
        {
            await control.PauseAsync(unit.Pass.Id, cancellationToken);
            PrerenderLog.PassPaused(logger, unit.Pass.Id, result.Reason ?? string.Empty);
        }

        return UnitEnd.Interrupted;
    }

    // Unknown to Data Dragon, the unit fails for good.
    private async Task<UnitEnd> UnknownAsync(ClaimedUnit unit, CancellationToken cancellationToken)
    {
        await queue.FinishAsync(unit, PrerenderUnitState.Failed, cancellationToken);
        PrerenderLog.UnitUnknown(logger, unit.Version.Value, unit.LocaleCode);
        return UnitEnd.Finished;
    }

    private async Task<string?> WhyWaitAsync(
        ClaimedUnit unit,
        CatalogLoadStatus status,
        CancellationToken cancellationToken)
    {
        if (status != CatalogLoadStatus.Ready)
        {
            return NotStored;
        }

        var from = await catalog.PinnedFromAsync(unit.Version, cancellationToken);
        return from is null ? NotPinned
            : from > clock.GetUtcNow() ? SettlingUntil(from.Value)
            : null;
    }

    // The first wait of a version is said once; past the longest wait, its units fail.
    private async Task<UnitEnd> WaitAsync(
        ClaimedUnit unit,
        string reason,
        CancellationToken cancellationToken)
    {
        if (waits.TryBegin(unit))
        {
            PrerenderLog.UnitWaiting(logger, unit.Version.Value, unit.Pass.Id, reason);
            return UnitEnd.Waiting;
        }

        if (!waits.HasExpired(unit))
        {
            return UnitEnd.Waiting;
        }

        waits.End(unit);
        await queue.AbandonAsync(unit, cancellationToken);
        var minutes = (int)UnitWaits.Longest.TotalMinutes;
        PrerenderLog.UnitAbandoned(logger, unit.Version.Value, unit.Pass.Id, minutes, reason);
        return UnitEnd.Finished;
    }

    private static string SettlingUntil(DateTimeOffset from) =>
        "it just left the latest rank, and the API and the SSR hold it for the latest patch"
        + $" until {from.ToString("O", CultureInfo.InvariantCulture)}.";

    // The unit's totals, its earlier runs included; the refusals and the time, of this run.
    private void LogCompleted(UnitWalk walk, long durationMs)
    {
        var (unit, tally) = (walk.Unit, walk.Tally);
        PrerenderLog.UnitCompleted(
            logger,
            unit.Version.Value,
            unit.LocaleCode,
            unit.PagesWritten + tally.Written,
            unit.PagesSkipped + tally.Skipped,
            unit.PagesFailed + tally.Failed,
            unit.BytesWritten + tally.Bytes,
            tally.Refusals,
            durationMs,
            tally.LastFailure ?? NoFailure);
    }

    private sealed record UnitWalk(ClaimedUnit Unit, IReadOnlyList<string> Paths, UnitTally Tally);
}
