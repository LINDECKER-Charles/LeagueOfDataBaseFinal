using LoDb.Api.Modules.Admin.Prerender.Telemetry;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Admin.Prerender.Passes;

/// <summary>
/// Moves a pass: paused, resumed or cancelled by an administrator, paused by the worker when
/// the store refuses its writer, completed by the worker once no unit is left.
/// </summary>
/// <remarks>
/// A move applies only to the state it was decided from: when the pass moved meanwhile (the
/// worker completed it while an administrator paused it, say), it is refused and nothing
/// changes. The worker sees a pause or a cancel before its next page, and completes a pass only
/// while none of its units is left.
/// </remarks>
internal sealed class PassControl(IDbContextFactory<LoDbDbContext> contexts, TimeProvider clock)
{
    public Task<PassChange> PauseAsync(long passId, CancellationToken cancellationToken) =>
        MoveAsync(passId, new PassTarget(PrerenderPassState.Paused), cancellationToken);

    /// <summary>
    /// Pauses the pass for <paramref name="reason"/>: the store or the SSR refuses it, and every
    /// page would fail alike. The reason shows until it runs again.
    /// </summary>
    public Task<PassChange> PauseItselfAsync(
        long passId,
        string reason,
        CancellationToken cancellationToken) =>
        MoveAsync(passId, new PassTarget(PrerenderPassState.Paused, reason), cancellationToken);

    public Task<PassChange> ResumeAsync(long passId, CancellationToken cancellationToken) =>
        MoveAsync(passId, new PassTarget(PrerenderPassState.Running), cancellationToken);

    public Task<PassChange> CancelAsync(long passId, CancellationToken cancellationToken) =>
        MoveAsync(passId, new PassTarget(PrerenderPassState.Cancelled), cancellationToken);

    public Task<PassChange> CompleteAsync(long passId, CancellationToken cancellationToken) =>
        MoveAsync(passId, new PassTarget(PrerenderPassState.Completed), cancellationToken);

    private async Task<PassChange> MoveAsync(
        long passId,
        PassTarget target,
        CancellationToken cancellationToken)
    {
        var to = target.State;
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var pass = await db.PrerenderPasses.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == passId, cancellationToken);
        if (pass is null)
        {
            return PassChange.NotFound;
        }

        var from = pass.State;
        if (!PassTimes.CanMove(from, to))
        {
            return PassChange.Refused;
        }

        PassTimes.Move(pass, to, clock.GetUtcNow());
        var reason = DiagnosticText.Clip(target.Reason, PrerenderPass.PauseReasonMaxLength);
        var moved = await Movable(db.PrerenderPasses.Where(row => row.Id == passId), from, to)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(row => row.State, pass.State)
                    .SetProperty(row => row.RunningSince, pass.RunningSince)
                    .SetProperty(row => row.ActiveSeconds, pass.ActiveSeconds)
                    .SetProperty(row => row.FinishedAt, pass.FinishedAt)
                    .SetProperty(row => row.PausedAt, pass.PausedAt)
                    .SetProperty(row => row.PauseReason, reason),
                cancellationToken);
        return moved == 1 ? PassChange.Changed : PassChange.Refused;
    }

    // The pass, if still in the state the move was decided from; to complete, if no unit is
    // left to render either: a promotion may add some after the worker found none.
    private static IQueryable<PrerenderPass> Movable(
        IQueryable<PrerenderPass> pass,
        PrerenderPassState from,
        PrerenderPassState to)
    {
        var unmoved = pass.Where(row => row.State == from);
        return to != PrerenderPassState.Completed
            ? unmoved
            : unmoved.Where(static row => !row.Units.Any(static unit =>
                unit.State == PrerenderUnitState.Pending
                || unit.State == PrerenderUnitState.Running));
    }
}
