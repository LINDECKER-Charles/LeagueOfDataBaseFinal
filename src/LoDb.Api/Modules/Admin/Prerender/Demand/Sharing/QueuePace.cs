using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pace;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Sharing;

/// <summary>
/// Holds the queue to the regulator's floor (<see cref="PaceOptions.MinRendersPerMinute"/>)
/// while no pass drives the regulator, in adaptive mode.
/// </summary>
/// <remarks>
/// Without a pass, <c>RenderPacer</c> answers the fixed pace, meant for a command line's one
/// unit, and nothing watches the host, the API nor the visitors' SSR. The queue renders around
/// the clock: it takes the pace the regulator itself falls back to when it cannot see. While a
/// pass drives the regulator, both share its pace at <c>RenderPacer</c> and this waits nothing;
/// in fixed mode, the operator chose the fixed pace, and this waits nothing either.
/// </remarks>
internal sealed class QueuePace(
    PaceRegulator regulator,
    IOptions<PrerenderOptions> options,
    TimeProvider clock)
{
    private readonly Lock _gate = new();
    private long? _nextStart;

    /// <summary>Waits until the queue may start its next render.</summary>
    public async Task WaitTurnAsync(CancellationToken cancellationToken)
    {
        if (Reserve() is { } wait && wait > TimeSpan.Zero)
        {
            await Task.Delay(wait, clock, cancellationToken);
        }
    }

    // The start this render takes, one floor interval after the one before it; null when the
    // regulator, or the operator's fixed pace, sets the pace.
    private TimeSpan? Reserve()
    {
        var pace = options.Value.Pace;
        if (!pace.IsAdaptive || regulator.IsDriven)
        {
            return null;
        }

        var interval = TimeSpan.FromMinutes(1) / pace.MinRendersPerMinute;
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
