using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Windows;

/// <summary>
/// The pass's renders over the last minute, as the API sees them around each call to its SSR:
/// their wall time, and the starts the pacer let through.
/// </summary>
/// <remarks>
/// Shared by the pages in flight and the regulator's sampler. Bounded: past
/// <see cref="Capacity"/> entries in a minute, the oldest go first.
/// </remarks>
internal sealed class PassRenderTimes(TimeProvider clock)
{
    /// <summary>Over the most renders a minute any setting allows.</summary>
    public const int Capacity = 8192;

    private readonly Lock _gate = new();
    private readonly Queue<(long At, double Seconds)> _renders = new();
    private readonly Queue<long> _starts = new();

    /// <summary>A render of the pass ended after <paramref name="duration"/>.</summary>
    public void Rendered(TimeSpan duration)
    {
        lock (_gate)
        {
            Keep(_renders, (clock.GetTimestamp(), duration.TotalSeconds));
            Trim(_renders, static entry => entry.At);
        }
    }

    /// <summary>The pacer let a render of the pass start.</summary>
    public void Started()
    {
        lock (_gate)
        {
            Keep(_starts, clock.GetTimestamp());
            Trim(_starts, static at => at);
        }
    }

    /// <summary>The renders and starts of the last minute.</summary>
    public PassRenderStats Measure()
    {
        lock (_gate)
        {
            var seconds = _renders.Where(entry => IsRecent(entry.At))
                .Select(static entry => entry.Seconds)
                .Order()
                .ToList();
            var median = seconds.Count == 0 ? (double?)null : MedianOf(seconds);
            return new PassRenderStats(seconds.Count, median, _starts.Count(IsRecent));
        }
    }

    private static double MedianOf(List<double> sorted) =>
        sorted.Count % 2 == 1
            ? sorted[sorted.Count / 2]
            : (sorted[(sorted.Count / 2) - 1] + sorted[sorted.Count / 2]) / 2;

    private static void Keep<T>(Queue<T> entries, T entry)
    {
        if (entries.Count == Capacity)
        {
            entries.Dequeue();
        }

        entries.Enqueue(entry);
    }

    private void Trim<T>(Queue<T> entries, Func<T, long> at)
    {
        while (entries.TryPeek(out var oldest) && !IsRecent(at(oldest)))
        {
            entries.Dequeue();
        }
    }

    private bool IsRecent(long at) => clock.GetElapsedTime(at) < PaceTiming.ShortWindow;
}
