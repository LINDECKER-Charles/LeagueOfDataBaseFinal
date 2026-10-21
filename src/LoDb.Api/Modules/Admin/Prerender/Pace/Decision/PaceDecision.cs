using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pace.Signals;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Decision;

/// <summary>
/// The regulator's rule, pure: additive increase while every signal is green, multiplicative
/// decrease on an amber one, a hold on a red one (docs/produit/pre-rendu-catalogue-cdn.md,
/// lot 2, « Comment il bouge »).
/// </summary>
internal static class PaceDecision
{
    private const int Halving = 2;

    /// <summary>The memory after <paramref name="evaluation"/>.</summary>
    public static PaceMemory Next(
        PaceMemory memory,
        PaceEvaluation evaluation,
        PaceOptions options)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(options);
        var worst = evaluation.Board.Worst;
        var noted = memory with
        {
            GreenStreak = worst.Level == SignalLevel.Green ? memory.GreenStreak + 1 : 0,
        };
        if (worst.Level == SignalLevel.Red)
        {
            return Held(noted, worst.Reason, evaluation.At);
        }

        if (noted.State == PaceState.Held)
        {
            return evaluation.At < noted.HeldUntil ? noted : Released(noted, evaluation, options);
        }

        return worst.Level switch
        {
            SignalLevel.Amber => Slowed(noted, evaluation, options),
            SignalLevel.Hold => noted.To(PaceState.Steady, worst.Reason, evaluation.At),
            _ => Green(noted, evaluation, options),
        };
    }

    // Extended at each red decision: the hold ends 5 minutes after the last one.
    private static PaceMemory Held(PaceMemory memory, PaceReason reason, DateTimeOffset at) =>
        memory.To(PaceState.Held, reason, at) with
        {
            Allowance = 0,
            HeldUntil = at + PaceTiming.Hold,
            LastDecreaseAt = at,
        };

    // Back at the floor, with the cooldown of a decrease before any increase.
    private static PaceMemory Released(
        PaceMemory memory,
        PaceEvaluation evaluation,
        PaceOptions options) =>
        memory.To(PaceState.Starting, ReasonOf(evaluation.Board.Worst), evaluation.At) with
        {
            Allowance = options.MinRendersPerMinute,
            HeldUntil = null,
            LastDecreaseAt = evaluation.At,
        };

    private static PaceMemory Slowed(
        PaceMemory memory,
        PaceEvaluation evaluation,
        PaceOptions options) =>
        memory.To(PaceState.Slowed, evaluation.Board.Worst.Reason, evaluation.At) with
        {
            Allowance = Math.Max(options.MinRendersPerMinute, memory.Allowance / Halving),
            LastDecreaseAt = evaluation.At,
        };

    private static PaceMemory Green(
        PaceMemory memory,
        PaceEvaluation evaluation,
        PaceOptions options)
    {
        var (at, ceiling) = (evaluation.At, options.MaxRendersPerMinute);
        if (at - memory.StartedAt < PaceTiming.WarmUp)
        {
            return memory with { Reason = PaceReason.WarmingUp };
        }

        if (memory.Allowance >= ceiling)
        {
            return memory.To(PaceState.Steady, PaceReason.Ceiling, at) with { Allowance = ceiling };
        }

        if (!MayIncrease(memory, evaluation))
        {
            var state = memory.State == PaceState.Starting
                ? PaceState.Starting
                : PaceState.Climbing;
            return memory.To(state, PaceReason.None, at);
        }

        var next = Math.Min(ceiling, memory.Allowance + options.StepRendersPerMinute);
        var reached = next == ceiling;
        return memory.To(
            reached ? PaceState.Steady : PaceState.Climbing,
            reached ? PaceReason.Ceiling : PaceReason.None,
            at) with { Allowance = next, LastIncreaseAt = at };
    }

    private static bool MayIncrease(PaceMemory memory, PaceEvaluation evaluation)
    {
        var at = evaluation.At;
        return memory.GreenStreak >= PaceTiming.GreenStreakToIncrease
            && at - memory.LastIncreaseAt >= PaceTiming.IncreaseSpacing
            && (memory.LastDecreaseAt is not { } decreased
                || at - decreased >= PaceTiming.DecreaseCooldown)
            && evaluation.AchievedStarts >= PaceTiming.AchievedShareToIncrease * memory.Allowance;
    }

    private static PaceReason ReasonOf(SignalReading worst) =>
        worst.Level == SignalLevel.Green ? PaceReason.None : worst.Reason;
}
