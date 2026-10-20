using System.Globalization;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Admin.Http;
using LoDb.Api.Modules.Admin.Prerender.Http.Views;
using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Passes;
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
    IPrerenderCatalog catalog,
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
    /// not configured here, while another pass is active, or when its perimeter holds no older
    /// patch.
    /// </summary>
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

        var ignored = await IgnoredAsync(launch.Scope, cancellationToken);
        if (ignored is null)
        {
            return (null, NonePinned());
        }

        var id = await launcher.StartAsync(launch, cancellationToken);
        return id is { } passId
            ? (await StartedAsync(passId, launch, ignored, cancellationToken), null)
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

    // The versions asked for that are not older patches, in the order sent; null when the
    // scope admits no older patch at all.
    // TODO(B1): PassPlanning.PlanAsync, which also leaves out what skipComplete finds held
    // (scope: nothing-to-render) and hands its plan to the launcher.
    private async Task<IReadOnlyList<string>?> IgnoredAsync(
        PassScope scope,
        CancellationToken cancellationToken)
    {
        var pinned = await catalog.PinnedVersionsAsync(cancellationToken);
        if (!pinned.Any(scope.Admits))
        {
            return null;
        }

        return [.. (scope.Versions ?? []).Except(pinned).Select(static version => version.Value)];
    }

    private static AccountProblem NonePinned()
    {
        var errors = new FieldErrors();
        errors.Add(PassRequestReader.VersionsField, PassRequestReader.NonePinned);
        return errors.ToProblem();
    }

    private async Task<PrerenderPassStarted> StartedAsync(
        long id,
        PassLaunch launch,
        IReadOnlyList<string> ignored,
        CancellationToken cancellationToken)
    {
        var started = new PrerenderPassStarted
        {
            Id = id,
            Epoch = launch.Epoch,
            Units = await db.PrerenderUnits.CountAsync(
                unit => unit.PassId == id,
                cancellationToken),
            SkippedUnits = 0,
            IgnoredVersions = ignored,
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
