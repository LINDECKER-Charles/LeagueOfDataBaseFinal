using LoDb.Api.Modules.Admin.Prerender.Options;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Rendering;

/// <summary>
/// Spaces the renders the pass asks for evenly, at <see cref="PrerenderOptions.RendersPerMinute"/>.
/// </summary>
/// <remarks>
/// The SSR admits the pass within a budget of its own and answers a 503 beyond it, with a
/// Retry-After of half a minute: a pass that ran as fast as it could would spend the lane's
/// burst, then wait most of its time. Held by one pass at a time, under the pass's lock, and
/// shared by the pages it has in flight together: each takes the next free start.
/// </remarks>
internal sealed class RenderPacer(IOptions<PrerenderOptions> options, TimeProvider clock)
{
    private readonly Lock _gate = new();
    private long? _nextStart;

    /// <summary>Waits until the next render may start, then counts it as started.</summary>
    public async Task WaitTurnAsync(CancellationToken cancellationToken)
    {
        var wait = Reserve();
        if (wait > TimeSpan.Zero)
        {
            await Task.Delay(wait, clock, cancellationToken);
        }
    }

    // The start this render takes, one interval after the one before it, and the wait until it.
    private TimeSpan Reserve()
    {
        var interval = TimeSpan.FromMinutes(1) / options.Value.RendersPerMinute;
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
