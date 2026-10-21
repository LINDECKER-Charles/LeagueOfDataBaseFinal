using System.Globalization;
using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;
using LoDb.Api.Modules.Admin.Prerender.Pace.Signals;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Telemetry;

/// <summary>
/// Turns a decision into its signs: a count per move, a log line per change of state, the
/// ceiling said once until the next decrease.
/// </summary>
internal sealed class PaceChanges(PaceMetrics metrics, ILogger logger)
{
    private const string NoValue = "none";
    private const string ValueFormat = "0.###";

    private bool _isCeilingAnnounced;

    /// <summary>Notes a move from <paramref name="before"/> to <paramref name="after"/>.</summary>
    public void Note(PaceMemory before, PaceMemory after, SignalReading worst)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(worst);
        // A release from a hold is counted as such, not as an increase.
        if (after.Allowance > before.Allowance && before.State != PaceState.Held)
        {
            metrics.Count(PaceChange.Increased, after.Reason);
        }

        if (after.State == PaceState.Held && before.State != PaceState.Held)
        {
            NoteHeld(after, worst);
        }
        else if (after.State == PaceState.Slowed)
        {
            NoteSlowed(before, after, worst);
        }

        NoteRecovered(before, after);
        NoteCeiling(after);
    }

    private void NoteHeld(PaceMemory after, SignalReading worst)
    {
        _isCeilingAnnounced = false;
        metrics.Count(PaceChange.Held, after.Reason);
        var (reason, value) = (PaceTags.TagOf(after.Reason), ValueOf(worst));
        PaceLog.Held(logger, reason, value, after.HeldUntil!.Value);
    }

    private void NoteSlowed(PaceMemory before, PaceMemory after, SignalReading worst)
    {
        if (after.Allowance < before.Allowance)
        {
            _isCeilingAnnounced = false;
            metrics.Count(PaceChange.Decreased, after.Reason);
        }

        if (before.State == PaceState.Slowed)
        {
            return;
        }

        var (reason, value) = (PaceTags.TagOf(after.Reason), ValueOf(worst));
        PaceLog.Slowed(logger, after.Allowance, reason, value);
    }

    private void NoteRecovered(PaceMemory before, PaceMemory after)
    {
        var wasHeld = before.State == PaceState.Held && after.State != PaceState.Held;
        var wasSlowed = before.State == PaceState.Slowed
            && after.State is not (PaceState.Slowed or PaceState.Held);
        if (wasHeld)
        {
            metrics.Count(PaceChange.Released, after.Reason);
        }

        if (wasHeld || wasSlowed)
        {
            var seconds = (long)(after.Since - before.Since).TotalSeconds;
            PaceLog.Recovered(logger, after.Allowance, seconds);
        }
    }

    private void NoteCeiling(PaceMemory after)
    {
        if (after.Reason == PaceReason.Ceiling && !_isCeilingAnnounced)
        {
            _isCeilingAnnounced = true;
            PaceLog.CeilingReached(logger, after.Allowance);
        }
    }

    private static string ValueOf(SignalReading worst) =>
        worst.Value?.ToString(ValueFormat, CultureInfo.InvariantCulture) ?? NoValue;
}
