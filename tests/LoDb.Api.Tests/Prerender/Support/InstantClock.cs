using System.Collections.Concurrent;

namespace LoDb.Api.Tests.Prerender.Support;

/// <summary>
/// The system's clock, whose waits end at once: every delay the pass asks for is recorded,
/// none is waited, and the clock moves on by it. A periodic timer keeps its period.
/// </summary>
internal sealed class InstantClock : TimeProvider
{
    private readonly ConcurrentQueue<TimeSpan> _waits = new();
    private long _offsetTicks;

    /// <summary>The delays asked for, in order: Retry-After, back-offs, the pace.</summary>
    public IReadOnlyCollection<TimeSpan> Waits => _waits;

    private TimeSpan Offset => TimeSpan.FromTicks(Interlocked.Read(ref _offsetTicks));

    /// <summary>Moves the clock on by <paramref name="duration"/>, as time passing would.</summary>
    public void Advance(TimeSpan duration) => Interlocked.Add(ref _offsetTicks, duration.Ticks);

    public override DateTimeOffset GetUtcNow() => System.GetUtcNow() + Offset;

    public override long GetTimestamp() =>
        System.GetTimestamp() + (long)(Offset.TotalSeconds * TimestampFrequency);

    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period)
    {
        if (period == Timeout.InfiniteTimeSpan && dueTime > TimeSpan.Zero)
        {
            _waits.Enqueue(dueTime);
            Advance(dueTime);
            dueTime = TimeSpan.Zero;
        }

        return System.CreateTimer(callback, state, dueTime, period);
    }
}
