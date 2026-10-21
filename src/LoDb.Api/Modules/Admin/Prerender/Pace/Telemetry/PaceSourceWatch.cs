namespace LoDb.Api.Modules.Admin.Prerender.Pace.Telemetry;

/// <summary>
/// Says once when a source of the regulator can no longer be read, and once when it can
/// again, however many evaluations it stays lost.
/// </summary>
internal sealed class PaceSourceWatch(TimeProvider clock, ILogger<PaceSourceWatch> logger)
{
    private readonly Dictionary<PaceSource, long> _lostSince = [];

    /// <summary>Notes whether <paramref name="source"/> could be read this evaluation.</summary>
    public void Note(PaceSource source, bool isReadable)
    {
        if (!isReadable)
        {
            if (_lostSince.TryAdd(source, clock.GetTimestamp()))
            {
                PaceLog.SignalLost(logger, source);
            }

            return;
        }

        if (_lostSince.Remove(source, out var since))
        {
            var seconds = (long)clock.GetElapsedTime(since).TotalSeconds;
            PaceLog.SignalRestored(logger, source, seconds);
        }
    }
}
