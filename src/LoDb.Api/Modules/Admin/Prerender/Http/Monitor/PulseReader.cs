using LoDb.Api.Modules.Admin.Prerender.Http.Views.Progress;
using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pace;
using LoDb.Api.Modules.Admin.Prerender.Rendering;
using LoDb.Api.Modules.Admin.Prerender.Units;
using LoDb.Api.Modules.Admin.Prerender.Units.Waits;
using LoDb.Api.Modules.Admin.Prerender.Writing;
using LoDb.Api.Workers;
using LoDb.Infrastructure.Persistence.Prerender;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Http.Monitor;

/// <summary>
/// What the instance answering sees of the engine, from its memory alone: when it last ran
/// it, whether the SSR is out of reach, which versions of the pass wait, and the pace its
/// regulator sets. None of it is stored, so another instance sees nothing.
/// </summary>
internal sealed class PulseReader(
    EnginePulse pulse,
    SsrOutage outage,
    UnitWaits waits,
    PaceRegulator regulator,
    IOptions<PrerenderOptions> options,
    IConfiguration configuration)
{
    private const int ShareDecimals = 3;

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
            Pace = pass.IsActive ? PaceOf(pass.Id) : null,
            Waiting = [.. waits.Snapshot(pass.Id).Select(ViewOf)],
        };
    }

    private PrerenderPaceView? PaceOf(long passId)
    {
        if (regulator.Snapshot() is not { } pace || pace.PassId != passId)
        {
            return null;
        }

        var bounds = options.Value.Pace;
        return new PrerenderPaceView
        {
            Mode = pace.Mode == PaceMode.Fixed ? "fixed" : "adaptive",
            State = PaceTags.TokenOf(pace.State),
            RendersPerMinute = pace.RendersPerMinute,
            MinRendersPerMinute = bounds.MinRendersPerMinute,
            MaxRendersPerMinute = bounds.MaxRendersPerMinute,
            Reason = PaceTags.TokenOf(pace.Reason),
            HostCpu = pace.HostBusy is { } busy ? Math.Round((decimal)busy, ShareDecimals) : null,
            HeldUntil = pace.HeldUntil,
            Since = pace.Since,
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
