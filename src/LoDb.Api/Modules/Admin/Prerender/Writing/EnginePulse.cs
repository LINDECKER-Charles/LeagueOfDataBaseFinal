namespace LoDb.Api.Modules.Admin.Prerender.Writing;

/// <summary>
/// When this instance last ran the engine: as it takes the pass's lock, and at each unit. The
/// admin tells a pass that waits on a worker from a pass that runs by it.
/// </summary>
/// <remarks>Held in memory: another instance, or this one after a restart, has seen none.</remarks>
internal sealed class EnginePulse(TimeProvider clock)
{
    private long _lastBeatTicks;

    /// <summary>When the engine last ran here; null when it never did.</summary>
    public DateTimeOffset? LastBeatAt => Interlocked.Read(ref _lastBeatTicks) is var ticks and > 0
        ? new DateTimeOffset(ticks, TimeSpan.Zero)
        : null;

    /// <summary>The engine runs now.</summary>
    public void Beat() => Interlocked.Exchange(ref _lastBeatTicks, clock.GetUtcNow().UtcTicks);
}
