using System.Net;
using LoDb.Api.Modules.Admin.Prerender.Builds;
using LoDb.Api.Modules.Admin.Prerender.Demand.Queue;
using LoDb.Api.Modules.Admin.Prerender.Demand.Sharing;
using LoDb.Api.Modules.Admin.Prerender.Rendering;
using LoDb.Api.Modules.Admin.Prerender.Writing;
using LoDb.Infrastructure.PageStore;
using LoDb.Infrastructure.PageStore.Errors;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Drain;

/// <summary>
/// One try of a page of the queue, by the pass's own path: a slot of the pass's SSR shared with
/// the passes (<see cref="RenderSlots"/>), a turn at the pass's pacer, the render, then its
/// build and the page written as a pass writes it (<see cref="PageObjects"/>, same key, same
/// metadata): a page of the queue is a page of the epoch like any other.
/// </summary>
/// <remarks>
/// The verdicts are the pass's (<see cref="RenderVerdicts"/>), but for a 200 without the
/// archived mark, a try that failed rather than a page skipped. An SSR out of reach, or an answer
/// that refuses every page (a setting), stops the drain until its next tick; the outage is
/// said once, with the pass's (<see cref="SsrOutage"/>).
/// </remarks>
internal sealed class DemandPageWriter(
    SsrClient ssr,
    RenderSlots slots,
    RenderPacer pacer,
    SsrOutage outage,
    BuildCopier builds,
    IPageStore store)
{
    private const string Unreachable = "The SSR is out of reach.";

    // A pass walks a unit whose catalogue its opening pages loaded; the queue's page often
    // opens its (version, locale), and a render that ran out of time on a cold catalogue
    // comes back as a page of a shorter class: tried again later, never skipped for good.
    private const string Unmarked =
        "The SSR answered 200 without the archived mark: its catalogue was likely still loading.";

    public async Task<DemandTry> TryAsync(PageJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        RenderedPage page;
        using (await slots.EnterAsync(RenderClaimant.Queue, cancellationToken))
        {
            if (!await pacer.WaitTurnAsync(cancellationToken))
            {
                return DemandTry.Held;
            }

            try
            {
                page = await ssr.RenderAsync(job.RequestPath, cancellationToken);
                outage.End();
            }
            catch (SsrUnavailableException exception)
            {
                return Unavailable(exception);
            }
        }

        return await JudgeAsync(job, page, cancellationToken);
    }

    private async Task<DemandTry> JudgeAsync(
        PageJob job,
        RenderedPage page,
        CancellationToken cancellationToken) =>
        RenderVerdicts.Of(page) switch
        {
            RenderVerdict.Storable => await StoreAsync(job, page, cancellationToken),
            RenderVerdict.Skipped when page.Status == HttpStatusCode.OK =>
                DemandTry.Failed(Unmarked),
            RenderVerdict.Skipped => DemandTry.Ended(DemandEnd.Skipped(
                $"The SSR answered {(int)page.Status}, a page that is never stored.")),
            RenderVerdict.Refused => DemandTry.Refused(RetryDelays.AfterRefusal(page.RetryAfter)),
            RenderVerdict.Misconfigured => DemandTry.Ended(DemandEnd.Stopped(
                $"The SSR answered {(int)page.Status} to {job.RequestPath}.")),
            _ => DemandTry.Failed($"The SSR answered {(int)page.Status}, not a page to store."),
        };

    private DemandTry Unavailable(SsrUnavailableException exception)
    {
        if (!exception.IsUnreachable)
        {
            return DemandTry.Failed(exception.Message);
        }

        outage.Begin();
        return DemandTry.Ended(DemandEnd.Stopped(Unreachable));
    }

    // The build first, then the page: a page is never in the store before its bundles.
    private async Task<DemandTry> StoreAsync(
        PageJob job,
        RenderedPage page,
        CancellationToken cancellationToken)
    {
        try
        {
            var build = page.Header(PrerenderHeaders.Build)!;
            if (await builds.EnsureAsync(build, cancellationToken) == BuildCopy.Superseded)
            {
                return DemandTry.Failed($"The SSR moved off build {build} while rendering.");
            }

            var written = PageObjects.Of(job, page);
            await store.PutAsync(written, cancellationToken);
            return DemandTry.Ended(DemandEnd.Written(written.Body.Length));
        }
        catch (SsrUnavailableException exception)
        {
            return Unavailable(exception);
        }
        catch (PageStoreTransientException exception)
        {
            return DemandTry.Failed(exception.Message);
        }
        catch (PageStoreConfigurationException exception)
        {
            return DemandTry.Ended(DemandEnd.Stopped(exception.Message));
        }
        catch (PageStoreRefusedException exception)
        {
            return DemandTry.Ended(DemandEnd.Failed(exception.Message, isLast: true));
        }
    }
}
