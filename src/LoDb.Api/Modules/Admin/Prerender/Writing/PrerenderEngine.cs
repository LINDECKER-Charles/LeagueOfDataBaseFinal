using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pace;
using LoDb.Api.Modules.Admin.Prerender.Passes;
using LoDb.Api.Modules.Admin.Prerender.Telemetry;
using LoDb.Api.Modules.Admin.Prerender.Units;
using LoDb.Infrastructure.Locks;

namespace LoDb.Api.Modules.Admin.Prerender.Writing;

/// <summary>
/// The pre-rendering pass at work (ADR 0011): unit after unit of the running pass, until none
/// is left and the pass is complete, or the pass stops.
/// </summary>
/// <remarks>
/// One instance writes at a time, under an advisory lock that the command line takes too.
/// Dormant until the pass and its store are configured (<see cref="PrerenderActivation"/>).
/// Each turn beats its <see cref="EnginePulse"/>, for the admin's monitor. Its pace is the
/// <see cref="PaceRegulator"/>'s while it drains a pass.
/// </remarks>
internal sealed class PrerenderEngine(
    PrerenderActivation activation,
    IDistributedLock locks,
    UnitQueue queue,
    UnitRunner units,
    PassControl control,
    EnginePulse pulse,
    PaceRegulator regulator,
    TimeProvider clock,
    ILogger<PrerenderEngine> logger)
{
    /// <summary>The advisory lock of whoever writes a pass.</summary>
    public const string LockName = "prerender:pass";

    /// <summary>Writes the running pass, if any, unless another instance does.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!activation.IsActive)
        {
            return;
        }

        await using var held = await locks.TryAcquireAsync(LockName, cancellationToken);
        if (held is not null)
        {
            pulse.Beat();
            await DriveAsync(cancellationToken);
        }
    }

    // The regulator sets the pace while this drain runs, from the floor; the command line,
    // which drains without it, keeps the fixed pace.
    private async Task DriveAsync(CancellationToken cancellationToken)
    {
        if (await queue.RunningPassAsync(cancellationToken) is not { } pass)
        {
            return;
        }

        regulator.Begin(pass.Id);
        try
        {
            await DrainAsync(cancellationToken);
        }
        finally
        {
            regulator.End();
        }
    }

    /// <summary>
    /// With <see cref="LockName"/> held: writes the running pass until it is complete, or it
    /// stops, or every unit left waits (<see cref="UnitEnd.Waiting"/>). A version that waits is
    /// tried again between two units (<see cref="WaitingVersions"/>).
    /// </summary>
    public async Task DrainAsync(CancellationToken cancellationToken)
    {
        var waiting = new WaitingVersions(clock);
        while (await queue.RunningPassAsync(cancellationToken) is { } pass)
        {
            pulse.Beat();
            var held = waiting.Held();
            if (await queue.NextAsync(pass, held, cancellationToken) is not { } unit)
            {
                if (held.Count > 0 || await CompleteAsync(pass, cancellationToken))
                {
                    return;
                }

                continue;
            }

            var end = await units.RunAsync(unit, cancellationToken);
            if (end == UnitEnd.Interrupted)
            {
                return;
            }

            if (end == UnitEnd.Waiting)
            {
                waiting.Hold(unit.Version.Value);
            }
        }
    }

    // Refused when units joined the pass since none was left (a promotion): they are next.
    private async Task<bool> CompleteAsync(RunningPass pass, CancellationToken cancellationToken)
    {
        if (await control.CompleteAsync(pass.Id, cancellationToken) != PassChange.Changed)
        {
            return false;
        }

        PrerenderLog.PassCompleted(logger, pass.Id, pass.Epoch);
        return true;
    }
}
