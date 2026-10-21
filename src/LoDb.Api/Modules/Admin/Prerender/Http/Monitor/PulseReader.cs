using LoDb.Api.Modules.Admin.Prerender.Http.Views.Progress;
using LoDb.Api.Modules.Admin.Prerender.Rendering;
using LoDb.Api.Modules.Admin.Prerender.Units;
using LoDb.Api.Modules.Admin.Prerender.Units.Waits;
using LoDb.Api.Modules.Admin.Prerender.Writing;
using LoDb.Api.Workers;
using LoDb.Infrastructure.Persistence.Prerender;

namespace LoDb.Api.Modules.Admin.Prerender.Http.Monitor;

/// <summary>
/// What the instance answering sees of the engine, from its memory alone: when it last ran
/// it, whether the SSR is out of reach, and which versions of the pass wait. None of it is
/// stored, so another instance sees nothing.
/// </summary>
internal sealed class PulseReader(
    EnginePulse pulse,
    SsrOutage outage,
    UnitWaits waits,
    IConfiguration configuration)
{
    /// <summary>The engine's signals for <paramref name="pass"/>.</summary>
    public PrerenderEnginePulse Of(PrerenderPass pass)
    {
        ArgumentNullException.ThrowIfNull(pass);
        return new PrerenderEnginePulse
        {
            ObservedAt = pulse.LastBeatAt,
            IsWorkerEnabled = ConventionalWorkers.IsEnabled(configuration),
            // The outage is the engine's, not a pass's: a pass over says nothing of it.
            SsrUnreachableSince = pass.IsActive ? outage.Since : null,
            Waiting = [.. waits.Snapshot(pass.Id).Select(ViewOf)],
        };
    }

    private static PrerenderWaitView ViewOf(UnitWait wait) => new()
    {
        Version = wait.Version,
        Reason = UnitWait.TextOf(wait.Reason),
        Since = wait.Since,
        GivesUpAt = wait.Since + UnitWaits.Longest,
    };
}
