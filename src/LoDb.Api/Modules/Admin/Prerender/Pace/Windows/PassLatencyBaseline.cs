using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Windows;

/// <summary>
/// The pass's usual render time: the lowest of its one-minute medians over the last 30
/// minutes, which a render that slows down is held against.
/// </summary>
internal sealed class PassLatencyBaseline
{
    private readonly Queue<(DateTimeOffset At, double Median)> _medians = new();

    public void Note(DateTimeOffset at, double medianSeconds)
    {
        _medians.Enqueue((at, medianSeconds));
        while (_medians.TryPeek(out var oldest) && at - oldest.At > PaceTiming.LongWindow)
        {
            _medians.Dequeue();
        }
    }

    /// <summary>The lowest median noted; null before the first.</summary>
    public double? Lowest() =>
        _medians.Count == 0 ? null : _medians.Min(static noted => noted.Median);

    public void Clear() => _medians.Clear();
}
