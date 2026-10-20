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
    PassLauncher launcher,
    PassControl control,
    PrerenderActivation activation,
    IOptions<PrerenderOptions> options,
    AdminSession session,
    IAuditLog audit)
{
    private const string EpochKey = "epoch";
    private const string ScopeKey = "scope";
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
    /// not configured here or while another pass is active.
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

        if (!activation.IsActive)
        {
            return (null, PrerenderProblems.Conflict(PrerenderProblems.Dormant));
        }

        var id = await launcher.StartAsync(launch, cancellationToken);
        if (id is not { } passId)
        {
            return (null, PrerenderProblems.Conflict(PrerenderProblems.PassActive));
        }

        await audit.RecordAsync(StartLine(passId, launch), cancellationToken);
        return (await StartedAsync(passId, launch.Epoch, cancellationToken), null);
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

    private async Task<PrerenderPassStarted> StartedAsync(
        long id,
        int epoch,
        CancellationToken cancellationToken) => new()
    {
        Id = id,
        Epoch = epoch,
        Units = await db.PrerenderUnits.CountAsync(unit => unit.PassId == id, cancellationToken),
    };

    private static AuditEvent StartLine(long id, PassLaunch launch) =>
        Line(AuditAction.AdminPrerenderStart, id, launch.Epoch) with
        {
            Meta = new Dictionary<string, object?>
            {
                [EpochKey] = launch.Epoch,
                [ScopeKey] = PassScopeJson.Write(launch.Scope),
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
