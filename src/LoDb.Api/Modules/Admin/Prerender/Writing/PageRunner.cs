using LoDb.Api.Modules.Admin.Prerender.Builds;
using LoDb.Api.Modules.Admin.Prerender.Rendering;
using LoDb.Api.Modules.Admin.Prerender.Telemetry;
using LoDb.Api.Modules.Admin.Prerender.Units;
using LoDb.Infrastructure.PageStore;
using LoDb.Infrastructure.PageStore.Errors;

namespace LoDb.Api.Modules.Admin.Prerender.Writing;

/// <summary>
/// Renders one page through the SSR and writes it to the store, trying again what may not
/// last: a page ends written, skipped or failed, or stays where it is when the pass stops.
/// </summary>
/// <remarks>
/// <para>
/// A refusal of the SSR (503, 429) is waited out as its Retry-After asks, as often as it comes:
/// it says the visitors need the room, not that the page is broken. So is an SSR out of reach
/// (connection refused, during a redeploy), uncounted but logged once (<see cref="SsrOutage"/>):
/// an outage must not fail every page of a unit one after the other. A failure that may not
/// last (a 5xx, a timeout, a store that throttles) is tried again a little later, then later
/// still, then counted failed (<see cref="RetryDelays"/>). A page the store refuses as it is
/// (metadata over the limit, say) fails at once.
/// </para>
/// <para>
/// Whether the pass still runs is read before each try: a pause or a cancel stops the page
/// there. An answer that refuses the pass itself ends the page unfinished, and the pass pauses.
/// </para>
/// </remarks>
internal sealed class PageRunner(
    SsrClient ssr,
    RenderPacer pacer,
    SsrOutage outage,
    BuildCopier builds,
    IPageStore store,
    UnitQueue queue,
    PrerenderMetrics metrics,
    TimeProvider clock)
{
    public async Task<PageResult> RunAsync(PageJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        var (failures, refusals) = (0, 0);
        while (await queue.IsRunningAsync(job.Unit.Pass.Id, cancellationToken))
        {
            var attempt = await AttemptAsync(job, cancellationToken);
            if (attempt.Result is { } result)
            {
                return Ended(result with { Refusals = refusals });
            }

            if (attempt.Failure is { } reason)
            {
                if (++failures >= RetryDelays.MaxAttempts)
                {
                    return Ended(PageResult.Failed(reason) with { Refusals = refusals });
                }
            }
            else if (!attempt.IsOutage)
            {
                refusals++;
                metrics.Count(PageOutcome.Refused);
            }

            await Task.Delay(attempt.WaitFor(failures), clock, cancellationToken);
        }

        return PageResult.Interrupted with { Refusals = refusals };
    }

    private PageResult Ended(PageResult result)
    {
        if (result.Outcome is { } outcome)
        {
            metrics.Count(outcome);
        }

        return result;
    }

    // One try, and what it says of the SSR's reach: any answer ends an outage.
    private async Task<Attempt> AttemptAsync(PageJob job, CancellationToken cancellationToken)
    {
        var attempt = await TryOnceAsync(job, cancellationToken);
        if (attempt.IsOutage)
        {
            outage.Begin();
        }
        else
        {
            outage.End();
        }

        return attempt;
    }

    private async Task<Attempt> TryOnceAsync(PageJob job, CancellationToken cancellationToken)
    {
        await pacer.WaitTurnAsync(cancellationToken);
        RenderedPage page;
        try
        {
            page = await ssr.RenderAsync(job.RequestPath, cancellationToken);
        }
        catch (SsrUnavailableException exception)
        {
            return exception.IsUnreachable ? Attempt.Outage : Attempt.Failed(exception.Message);
        }

        return RenderVerdicts.Of(page) switch
        {
            RenderVerdict.Storable => await StoreAsync(job, page, cancellationToken),
            RenderVerdict.Skipped => Attempt.Ended(PageResult.Skipped),
            RenderVerdict.Refused => Attempt.Refused(RetryDelays.AfterRefusal(page.RetryAfter)),
            RenderVerdict.Misconfigured => Attempt.Ended(PageResult.Misconfigured(
                $"The SSR answered {(int)page.Status} to {job.RequestPath}.")),
            _ => Attempt.Failed($"The SSR answered {(int)page.Status}, not a page to store."),
        };
    }

    // The build first, then the page: a page is never in the store before its bundles.
    private async Task<Attempt> StoreAsync(
        PageJob job,
        RenderedPage page,
        CancellationToken cancellationToken)
    {
        try
        {
            var build = page.Header(PrerenderHeaders.Build)!;
            if (await builds.EnsureAsync(build, cancellationToken) == BuildCopy.Superseded)
            {
                return Attempt.Again($"The SSR moved off build {build} while rendering.");
            }

            var written = PageObjects.Of(job, page);
            await store.PutAsync(written, cancellationToken);
            return Attempt.Ended(PageResult.Written(written.Body.Length));
        }
        catch (SsrUnavailableException exception) when (exception.IsUnreachable)
        {
            return Attempt.Outage;
        }
        catch (Exception exception) when (exception
            is PageStoreTransientException
            or SsrUnavailableException)
        {
            return Attempt.Failed(exception.Message);
        }
        catch (PageStoreConfigurationException exception)
        {
            return Attempt.Ended(PageResult.Misconfigured(exception.Message));
        }
        catch (PageStoreRefusedException exception)
        {
            return Attempt.Ended(PageResult.Failed(exception.Message));
        }
    }

    // One try of a page: it ended, or it is tried again after a refusal (no reason) or after a
    // failure (its reason), at once when the failure is only a build that changed, or after an
    // outage of the SSR.
    private sealed record Attempt(PageResult? Result, string? Failure, TimeSpan? Wait)
    {
        public static Attempt Outage { get; } =
            new(null, null, RetryDelays.Default) { IsOutage = true };

        public bool IsOutage { get; private init; }

        public static Attempt Ended(PageResult result) => new(result, null, null);

        public static Attempt Refused(TimeSpan wait) => new(null, null, wait);

        public static Attempt Failed(string reason) => new(null, reason, null);

        public static Attempt Again(string reason) => new(null, reason, TimeSpan.Zero);

        public TimeSpan WaitFor(int failures) => Wait ?? RetryDelays.AfterFailure(failures);
    }
}
