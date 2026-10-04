namespace LoDb.Ingestion.Tests.Pipeline;

/// <summary>
/// A wall clock moved by hand over the real timers and timestamps.
/// </summary>
/// <remarks>
/// The egress's resilience pipeline takes its clock from the container: a fake clock would
/// freeze its retry delays. Only <see cref="GetUtcNow"/> is faked, so the dates the pipeline
/// writes stay exact while every delay still elapses.
/// </remarks>
internal sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    private readonly Lock gate = new();
    private DateTimeOffset now = start;

    public override DateTimeOffset GetUtcNow()
    {
        lock (gate)
        {
            return now;
        }
    }

    public void Advance(TimeSpan delta)
    {
        lock (gate)
        {
            now += delta;
        }
    }
}
