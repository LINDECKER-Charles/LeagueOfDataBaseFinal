using LoDb.Api.Modules.Admin.Prerender.Pace;
using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;
using LoDb.Api.Modules.Admin.Prerender.Pace.Windows;

namespace LoDb.Api.Modules.Admin.Prerender.Rendering;

/// <summary>
/// Spaces the renders the pass asks for evenly, at the allowance of the
/// <see cref="PaceRegulator"/>: the regulator's while the engine drains a pass, the fixed pace
/// otherwise.
/// </summary>
/// <remarks>
/// The SSR admits the pass within a budget of its own and answers a 503 beyond it, with a
/// Retry-After of half a minute: a pass that ran as fast as it could would spend the lane's
/// burst, then wait most of its time. Held by one pass at a time, under the pass's lock, and
/// shared by the pages it has in flight together: each takes the next free start, spaced by the
/// allowance read as it reserves it.
/// </remarks>
internal sealed class RenderPacer(
    PaceRegulator regulator,
    PassRenderTimes renders,
    TimeProvider clock)
{
    private readonly Lock _gate = new();
    private long? _nextStart;

    /// <summary>
    /// Waits until the next render may start, counts it as started and returns true; or, while
    /// the regulator holds the pass, waits <see cref="PaceTiming.HeldRecheck"/> at most and
    /// returns false without reserving anything, so that the page looks again whether its pass
    /// still runs.
    /// </summary>
    public async Task<bool> WaitTurnAsync(CancellationToken cancellationToken)
    {
        if (Reserve() is not { } wait)
        {
            await Task.Delay(PaceTiming.HeldRecheck, clock, cancellationToken);
            return false;
        }

        if (wait > TimeSpan.Zero)
        {
            await Task.Delay(wait, clock, cancellationToken);
        }

        renders.Started();
        return true;
    }

    // The start this render takes, one interval after the one before it, and the wait until
    // it; null while the allowance is nought.
    private TimeSpan? Reserve()
    {
        var rendersPerMinute = regulator.Allowance();
        if (rendersPerMinute <= 0)
        {
            return null;
        }

        var interval = TimeSpan.FromMinutes(1) / rendersPerMinute;
        var intervalTicks = (long)(interval.TotalSeconds * clock.TimestampFrequency);
        lock (_gate)
        {
            var now = clock.GetTimestamp();
            var start = _nextStart is { } next && next > now ? next : now;
            _nextStart = start + intervalTicks;
            return clock.GetElapsedTime(now, start);
        }
    }
}
