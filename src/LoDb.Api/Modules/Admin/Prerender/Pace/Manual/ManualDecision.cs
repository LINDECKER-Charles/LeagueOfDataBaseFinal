using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;
using LoDb.Api.Modules.Admin.Prerender.Pace.Signals;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Manual;

/// <summary>
/// The manual mode's rule, pure: the operator's target, whatever the load, but for the
/// emergency brake, which holds every new render for <see cref="PaceTiming.Hold"/> from its
/// last braking decision (docs/produit/pre-rendu-catalogue-cdn.md, « Vitesse réglable »).
/// </summary>
/// <remarks>
/// The brake engages at once when the visitors' latest-patch lane refuses one of them, and
/// after <see cref="UnsafeDecisionsToBrake"/> decisions in a row (3 minutes) on a host busy at
/// <see cref="BrakeHostBusy"/> or more, steal included, or without the visitors' load to read.
/// The refusals of the older patches' lane are not a sign of load: that lane refuses past its
/// budget whatever the host does.
/// </remarks>
internal static class ManualDecision
{
    /// <summary>
    /// The host's busy share, steal included, over a minute, that the brake reads.
    /// </summary>
    public const double BrakeHostBusy = 0.90;

    /// <summary>Unsafe decisions in a row before the brake: 3 minutes at one every 30 s.</summary>
    public const int UnsafeDecisionsToBrake = 6;

    /// <summary>
    /// The memory after <paramref name="evaluation"/>, the target being
    /// <paramref name="target"/>; an adaptive memory starts the manual drain afresh.
    /// </summary>
    public static PaceMemory Next(PaceMemory memory, PaceEvaluation evaluation, int target)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(evaluation);
        var at = evaluation.At;
        var manual = memory.IsManual ? memory : PaceMemory.Manual(at, target);
        var board = evaluation.Board;
        if (board.VisitorsRefused is > 0)
        {
            return Braked(manual, PaceReason.VisitorsRefused, at);
        }

        var unsafeReason = UnsafeReasonOf(board);
        var noted = manual with
        {
            UnsafeStreak = unsafeReason is null ? 0 : manual.UnsafeStreak + 1,
        };
        if (unsafeReason is { } reason && noted.UnsafeStreak >= UnsafeDecisionsToBrake)
        {
            return Braked(noted, reason, at);
        }

        if (noted.State == PaceState.Held && at < noted.HeldUntil)
        {
            return noted;
        }

        return noted.To(PaceState.Manual, PaceReason.Manual, at) with
        {
            Allowance = target,
            HeldUntil = null,
        };
    }

    /// <summary>What the brake reads of <paramref name="memory"/>'s reason, for the logs.</summary>
    public static SignalReading ReadingOf(PaceMemory memory, PaceBoard board)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(board);
        double? value = memory.Reason switch
        {
            PaceReason.HostCpu => board.HostBusy,
            PaceReason.VisitorsRefused => board.VisitorsRefused,
            _ => null,
        };
        var level = memory.State == PaceState.Held ? SignalLevel.Red : SignalLevel.Green;
        return new SignalReading(level, memory.Reason, value);
    }

    private static PaceReason? UnsafeReasonOf(PaceBoard board) =>
        board.VisitorsRefused is null || board.HostBusy is null ? PaceReason.SignalMissing
        : board.HostBusy >= BrakeHostBusy ? PaceReason.HostCpu
        : null;

    // Extended at each braking decision; the streak starts again once the hold is over.
    private static PaceMemory Braked(PaceMemory memory, PaceReason reason, DateTimeOffset at) =>
        memory.To(PaceState.Held, reason, at) with
        {
            Allowance = 0,
            HeldUntil = at + PaceTiming.Hold,
            LastDecreaseAt = at,
            UnsafeStreak = 0,
        };
}
