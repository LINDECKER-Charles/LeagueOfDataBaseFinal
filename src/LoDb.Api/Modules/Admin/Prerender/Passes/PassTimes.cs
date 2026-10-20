using LoDb.Infrastructure.Persistence.Prerender;

namespace LoDb.Api.Modules.Admin.Prerender.Passes;

/// <summary>
/// The moves a pass may make, and what each does to its clock: a pass runs, pauses and
/// resumes, then is completed by the worker or cancelled by an administrator.
/// </summary>
/// <remarks>
/// Each stretch of running time is added to <see cref="PrerenderPass.ActiveSeconds"/> when it
/// closes, so that the admin's rate leaves the paused time out.
/// </remarks>
internal static class PassTimes
{
    public static bool CanMove(PrerenderPassState from, PrerenderPassState to) => (from, to) switch
    {
        (PrerenderPassState.Running, PrerenderPassState.Paused) => true,
        (PrerenderPassState.Running, PrerenderPassState.Completed) => true,
        (PrerenderPassState.Running, PrerenderPassState.Cancelled) => true,
        (PrerenderPassState.Paused, PrerenderPassState.Running) => true,
        (PrerenderPassState.Paused, PrerenderPassState.Cancelled) => true,
        _ => false,
    };

    /// <summary>
    /// Moves <paramref name="pass"/> to <paramref name="to"/> at <paramref name="now"/>.
    /// </summary>
    public static void Move(PrerenderPass pass, PrerenderPassState to, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(pass);
        if (pass.RunningSince is { } since)
        {
            pass.ActiveSeconds += Math.Max(0, (long)(now - since).TotalSeconds);
        }

        pass.State = to;
        pass.RunningSince = to == PrerenderPassState.Running ? now : null;
        pass.FinishedAt = to is PrerenderPassState.Completed or PrerenderPassState.Cancelled
            ? now
            : null;
    }
}
