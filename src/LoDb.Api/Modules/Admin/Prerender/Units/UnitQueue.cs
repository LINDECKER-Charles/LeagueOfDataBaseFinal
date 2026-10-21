using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Passes;
using LoDb.Api.Modules.Admin.Prerender.Telemetry;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Admin.Prerender.Units;

/// <summary>
/// The worker's reads and writes of the units of the running pass: which comes next, where
/// each stands, page after page.
/// </summary>
/// <remarks>
/// A page's outcome and the unit's cursor are saved together, with the pass's counters, in one
/// transaction: a pass resumes on the page after the last one counted, across restarts and
/// deploys, and the counters of the pass stay the sums over its units. A unit's start is kept
/// from its first run, its end written once, and the reason of its last failure kept for the
/// admin.
/// </remarks>
internal sealed class UnitQueue(IDbContextFactory<LoDbDbContext> contexts, TimeProvider clock)
{
    /// <summary>The pass the worker writes now, if any.</summary>
    public async Task<RunningPass?> RunningPassAsync(CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var row = await db.PrerenderPasses.AsNoTracking()
            .Where(static pass => pass.State == PrerenderPassState.Running)
            .Select(static pass => new { pass.Id, pass.Epoch, pass.Scope })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null
            ? null
            : new RunningPass(row.Id, row.Epoch, PassScopeJson.Read(row.Scope));
    }

    /// <summary>Whether <paramref name="passId"/> still runs: neither paused nor over.</summary>
    public async Task<bool> IsRunningAsync(long passId, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        return await db.PrerenderPasses.AnyAsync(
            pass => pass.Id == passId && pass.State == PrerenderPassState.Running,
            cancellationToken);
    }

    /// <summary>
    /// The unit to render next: the one a restart left running, else the first pending one;
    /// none of the <paramref name="waiting"/> versions, whose units wait for their catalog.
    /// </summary>
    public async Task<ClaimedUnit?> NextAsync(
        RunningPass pass,
        IReadOnlyCollection<string> waiting,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(pass);
        ArgumentNullException.ThrowIfNull(waiting);
        await using var db = await contexts.CreateDbContextAsync(token);
        var row = await db.PrerenderUnits.AsNoTracking()
            .Where(unit => unit.PassId == pass.Id
                && (unit.State == PrerenderUnitState.Running
                    || unit.State == PrerenderUnitState.Pending)
                && !waiting.Contains(unit.Version))
            .OrderByDescending(static unit => unit.State == PrerenderUnitState.Running)
            .ThenBy(static unit => unit.Priority)
            .ThenBy(static unit => unit.Version)
            .ThenBy(static unit => unit.Locale)
            .FirstOrDefaultAsync(token);
        return row is null ? null : ClaimedOf(pass, row);
    }

    /// <summary>
    /// Marks <paramref name="unit"/> running over <paramref name="pagesTotal"/> pages.
    /// </summary>
    public async Task StartAsync(ClaimedUnit unit, int pagesTotal, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(unit);
        var added = pagesTotal - unit.PagesTotal;
        var now = clock.GetUtcNow();
        await using var db = await contexts.CreateDbContextAsync(token);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await UnitRow(db, unit).ExecuteUpdateAsync(
            setters => setters
                .SetProperty(row => row.State, PrerenderUnitState.Running)
                .SetProperty(row => row.PagesTotal, pagesTotal)
                .SetProperty(row => row.StartedAt, row => row.StartedAt ?? now)
                .SetProperty(row => row.UpdatedAt, now),
            token);
        await PassRow(db, unit).ExecuteUpdateAsync(
            setters => setters.SetProperty(row => row.PagesTotal, row => row.PagesTotal + added),
            token);
        await transaction.CommitAsync(token);
    }

    /// <summary>Counts pages of <paramref name="unit"/> and moves its cursor on.</summary>
    public async Task RecordAsync(ClaimedUnit unit, UnitStep step, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(step);
        var (written, skipped, failed) = (step.Written, step.Skipped, step.Failed);
        var failure = DiagnosticText.Clip(step.Failure, PrerenderUnit.FailureMaxLength);
        await using var db = await contexts.CreateDbContextAsync(token);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await UnitRow(db, unit).ExecuteUpdateAsync(
            setters => setters
                .SetProperty(row => row.PagesWritten, row => row.PagesWritten + written)
                .SetProperty(row => row.PagesSkipped, row => row.PagesSkipped + skipped)
                .SetProperty(row => row.PagesFailed, row => row.PagesFailed + failed)
                .SetProperty(row => row.BytesWritten, row => row.BytesWritten + step.Bytes)
                .SetProperty(row => row.Cursor, step.Cursor)
                .SetProperty(row => row.Failure, row => failure ?? row.Failure)
                .SetProperty(row => row.UpdatedAt, clock.GetUtcNow()),
            token);
        await PassRow(db, unit).ExecuteUpdateAsync(
            setters => setters
                .SetProperty(row => row.PagesWritten, row => row.PagesWritten + written)
                .SetProperty(row => row.PagesSkipped, row => row.PagesSkipped + skipped)
                .SetProperty(row => row.PagesFailed, row => row.PagesFailed + failed)
                .SetProperty(row => row.BytesWritten, row => row.BytesWritten + step.Bytes),
            token);
        await transaction.CommitAsync(token);
    }

    /// <summary>Ends <paramref name="unit"/>, completed or failed for good.</summary>
    public Task FinishAsync(ClaimedUnit unit, PrerenderUnitState state, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(unit);
        return EndAsync(UnitRows.Of(unit), new UnitEnding(state, null), token);
    }

    /// <summary>Fails <paramref name="unit"/> for good, for <paramref name="reason"/>.</summary>
    public Task FailAsync(ClaimedUnit unit, string reason, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(unit);
        return EndAsync(
            UnitRows.Of(unit),
            new UnitEnding(PrerenderUnitState.Failed, reason),
            token);
    }

    /// <summary>
    /// Fails the units of <paramref name="unit"/>'s version in its pass that did not end, for
    /// <paramref name="reason"/>: their catalog never came, or the version never became an
    /// older patch.
    /// </summary>
    public Task AbandonAsync(ClaimedUnit unit, string reason, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(unit);
        return EndAsync(
            UnitRows.LeftOf(unit),
            new UnitEnding(PrerenderUnitState.Failed, reason),
            token);
    }

    // The end is written once: a unit that ended keeps its first end.
    private async Task EndAsync(
        Func<LoDbDbContext, IQueryable<PrerenderUnit>> rows,
        UnitEnding ending,
        CancellationToken token)
    {
        var now = clock.GetUtcNow();
        var failure = DiagnosticText.Clip(ending.Reason, PrerenderUnit.FailureMaxLength);
        await using var db = await contexts.CreateDbContextAsync(token);
        await rows(db).ExecuteUpdateAsync(
            setters => setters
                .SetProperty(row => row.State, ending.State)
                .SetProperty(row => row.Failure, row => failure ?? row.Failure)
                .SetProperty(row => row.EndedAt, row => row.EndedAt ?? now)
                .SetProperty(row => row.UpdatedAt, now),
            token);
    }

    private static IQueryable<PrerenderUnit> UnitRow(LoDbDbContext db, ClaimedUnit unit) =>
        UnitRows.Of(unit)(db);

    private static IQueryable<PrerenderPass> PassRow(LoDbDbContext db, ClaimedUnit unit)
    {
        var passId = unit.Pass.Id;
        return db.PrerenderPasses.Where(row => row.Id == passId);
    }

    private static ClaimedUnit ClaimedOf(RunningPass pass, PrerenderUnit row) => new()
    {
        Pass = pass,
        Version = PatchVersion.Parse(row.Version),
        Locale = UiLocales.TryParse(row.Locale, out var locale)
            ? locale
            : throw new FormatException($"'{row.Locale}' is not a locale of the site."),
        Cursor = row.Cursor,
        PagesTotal = row.PagesTotal,
        PagesWritten = row.PagesWritten,
        PagesSkipped = row.PagesSkipped,
        PagesFailed = row.PagesFailed,
        BytesWritten = row.BytesWritten,
    };

    // How a unit ends, and why when it fails.
    private sealed record UnitEnding(PrerenderUnitState State, string? Reason);

    // The rows a write of the queue reaches.
    private static class UnitRows
    {
        public static Func<LoDbDbContext, IQueryable<PrerenderUnit>> Of(ClaimedUnit unit)
        {
            var (passId, version, locale) = (unit.Pass.Id, unit.Version.Value, unit.LocaleCode);
            return db => db.PrerenderUnits.Where(row =>
                row.PassId == passId && row.Version == version && row.Locale == locale);
        }

        // The units of the version in the pass that did not end.
        public static Func<LoDbDbContext, IQueryable<PrerenderUnit>> LeftOf(ClaimedUnit unit)
        {
            var (passId, version) = (unit.Pass.Id, unit.Version.Value);
            return db => db.PrerenderUnits.Where(row => row.PassId == passId
                && row.Version == version
                && (row.State == PrerenderUnitState.Pending
                    || row.State == PrerenderUnitState.Running));
        }
    }
}
