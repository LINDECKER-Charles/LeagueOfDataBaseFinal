using System.Globalization;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Admin.Http;
using LoDb.Api.Modules.Admin.Prerender.Http.Views;
using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Passes;
using LoDb.Api.Modules.Admin.Prerender.Passes.Planning;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Http;

/// <summary>
/// Launches, pauses, resumes and cancels the passes of the pre-rendering from the admin, each
/// action recorded in the audit journal once it applied.
/// </summary>
/// <remarks>
/// The worker sees a pause or a cancel before its next page, and takes a new or resumed pass
/// at its next tick. There is no switch of the served epoch here: nginx reads it at its start
/// (<c>LODB_ARCHIVE_SERVE_EPOCH</c>), so it changes with a redeploy (ADR 0011).
/// </remarks>
internal sealed class PrerenderDesk(
    LoDbDbContext db,
    PassPlanning planning,
    PassLauncher launcher,
    PassControl control,
    PrerenderActivation activation,
    IOptions<PrerenderOptions> options,
    AdminSession session,
    IAuditLog audit)
{
    private const string EpochKey = "epoch";
    private const string ScopeKey = "scope";
    private const string UnitsKey = "units";
    private const string SkippedKey = "skipped";
    private const string LabelPrefix = "epoch ";

    private static readonly PassMove Pause = new(
        static (control, id, token) => control.PauseAsync(id, token),
        AuditAction.AdminPrerenderPause);

    private static readonly PassMove Resume = new(
        static (control, id, token) => control.ResumeAsync(id, token),
        AuditAction.AdminPrerenderResume);

    private static readonly PassMove Cancel = new(
        static (control, id, token) => control.CancelAsync(id, token),
        AuditAction.AdminPrerenderCancel);

    /// <summary>
    /// Opens the pass <paramref name="request"/> asks for; refused while the pre-rendering is
    /// not configured here, while another pass is active, or when it has nothing to render.
    /// </summary>
    /// <remarks>
    /// In this order: the request read whole, the host and the active pass checked before the
    /// cost of planning, then the plan, refused when empty before any row is written.
    /// </remarks>
    public async Task<(PrerenderPassStarted? Started, AccountProblem? Problem)> StartAsync(
        PassRequest request,
        CancellationToken cancellationToken)
    {
        var (launch, problem) = PassRequestReader.Read(
            request,
            options.Value.ServedEpoch,
            session.CurrentId);
        if (launch is null)
        {
            return (null, problem);
        }

        if (await RefusalAsync(cancellationToken) is { } refusal)
        {
            return (null, refusal);
        }

        var plan = await planning.PlanAsync(launch, cancellationToken);
        if (plan.Units.Count == 0)
        {
            return (null, Empty(plan));
        }

        var id = await launcher.StartAsync(launch, plan, cancellationToken);
        return id is { } passId
            ? (await StartedAsync(passId, launch, plan, cancellationToken), null)
            : (null, PrerenderProblems.Conflict(PrerenderProblems.PassActive));
    }

    public Task<AccountProblem?> PauseAsync(long id, CancellationToken cancellationToken) =>
        MoveAsync(id, Pause, cancellationToken);

    public Task<AccountProblem?> ResumeAsync(long id, CancellationToken cancellationToken) =>
        MoveAsync(id, Resume, cancellationToken);

    public Task<AccountProblem?> CancelAsync(long id, CancellationToken cancellationToken) =>
        MoveAsync(id, Cancel, cancellationToken);

    private async Task<AccountProblem?> MoveAsync(
        long id,
        PassMove move,
        CancellationToken cancellationToken)
    {
        var epoch = await db.PrerenderPasses.AsNoTracking()
            .Where(pass => pass.Id == id)
            .Select(static pass => (int?)pass.Epoch)
            .SingleOrDefaultAsync(cancellationToken);
        if (epoch is not { } written)
        {
            return PrerenderProblems.NotFound();
        }

        switch (await move.Apply(control, id, cancellationToken))
        {
            case PassChange.NotFound:
                return PrerenderProblems.NotFound();
            case PassChange.Refused:
                return PrerenderProblems.Conflict(PrerenderProblems.PassState);
            default:
                await audit.RecordAsync(Line(move.Action, id, written), cancellationToken);
                return null;
        }
    }

    // Before planning, which reads the catalog: the launcher checks it again, in a race.
    private async Task<AccountProblem?> RefusalAsync(CancellationToken cancellationToken)
    {
        if (!activation.IsActive)
        {
            return PrerenderProblems.Conflict(PrerenderProblems.Dormant);
        }

        var isTaken = await db.PrerenderPasses.AnyAsync(
            static pass => pass.IsActive,
            cancellationToken);
        return isTaken ? PrerenderProblems.Conflict(PrerenderProblems.PassActive) : null;
    }

    // Nothing skipped: no version asked for, or none at all, is an older patch. Else every
    // unit asked for is complete already.
    private static AccountProblem Empty(PassPlan plan)
    {
        var errors = new FieldErrors();
        if (plan.Skipped == 0)
        {
            errors.Add(PassRequestReader.VersionsField, PassRequestReader.NonePinned);
        }
        else
        {
            errors.Add(PassRequestReader.ScopeField, PassRequestReader.NothingToRender);
        }

        return errors.ToProblem();
    }

    private async Task<PrerenderPassStarted> StartedAsync(
        long id,
        PassLaunch launch,
        PassPlan plan,
        CancellationToken cancellationToken)
    {
        var started = new PrerenderPassStarted
        {
            Id = id,
            Epoch = launch.Epoch,
            Units = plan.Units.Count,
            SkippedUnits = plan.Skipped,
            IgnoredVersions = plan.Ignored,
        };
        await audit.RecordAsync(StartLine(started, launch.Scope), cancellationToken);
        return started;
    }

    private static AuditEvent StartLine(PrerenderPassStarted started, PassScope scope) =>
        Line(AuditAction.AdminPrerenderStart, started.Id, started.Epoch) with
        {
            Meta = new Dictionary<string, object?>
            {
                [EpochKey] = started.Epoch,
                [ScopeKey] = PassScopeJson.Write(scope),
                [UnitsKey] = started.Units,
                [SkippedKey] = started.SkippedUnits,
            },
        };

    private static AuditEvent Line(AuditAction action, long id, int epoch) => new()
    {
        Action = action,
        Target = new AuditTarget(
            AuditTargetType.PrerenderPass,
            id.ToString(CultureInfo.InvariantCulture),
            LabelPrefix + epoch.ToString(CultureInfo.InvariantCulture)),
        Meta = new Dictionary<string, object?> { [EpochKey] = epoch },
    };

    // A move of the pass control, and the action that records it.
    private sealed record PassMove(
        Func<PassControl, long, CancellationToken, Task<PassChange>> Apply,
        AuditAction Action);
}
