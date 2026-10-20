using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Passes;
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
/// deploys, and the counters of the pass stay the sums over its units.
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
        await using var db = await contexts.CreateDbContextAsync(token);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await UnitRow(db, unit).ExecuteUpdateAsync(
            setters => setters
                .SetProperty(row => row.State, PrerenderUnitState.Running)
                .SetProperty(row => row.PagesTotal, pagesTotal)
                .SetProperty(row => row.UpdatedAt, clock.GetUtcNow()),
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
        await using var db = await contexts.CreateDbContextAsync(token);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await UnitRow(db, unit).ExecuteUpdateAsync(
            setters => setters
                .SetProperty(row => row.PagesWritten, row => row.PagesWritten + written)
                .SetProperty(row => row.PagesSkipped, row => row.PagesSkipped + skipped)
                .SetProperty(row => row.PagesFailed, row => row.PagesFailed + failed)
                .SetProperty(row => row.BytesWritten, row => row.BytesWritten + step.Bytes)
                .SetProperty(row => row.Cursor, step.Cursor)
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
    public async Task FinishAsync(
        ClaimedUnit unit,
        PrerenderUnitState state,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(unit);
        await using var db = await contexts.CreateDbContextAsync(token);
        await UnitRow(db, unit).ExecuteUpdateAsync(
            setters => setters
                .SetProperty(row => row.State, state)
                .SetProperty(row => row.UpdatedAt, clock.GetUtcNow()),
            token);
    }

    /// <summary>
    /// Fails the units of <paramref name="unit"/>'s version in its pass that did not end: their
    /// catalog never came, or the version never became an older patch.
    /// </summary>
    public async Task AbandonAsync(ClaimedUnit unit, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(unit);
        var (passId, version) = (unit.Pass.Id, unit.Version.Value);
        await using var db = await contexts.CreateDbContextAsync(token);
        await db.PrerenderUnits
            .Where(row => row.PassId == passId
                && row.Version == version
                && (row.State == PrerenderUnitState.Pending
                    || row.State == PrerenderUnitState.Running))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(row => row.State, PrerenderUnitState.Failed)
                    .SetProperty(row => row.UpdatedAt, clock.GetUtcNow()),
                token);
    }

    private static IQueryable<PrerenderUnit> UnitRow(LoDbDbContext db, ClaimedUnit unit)
    {
        var (passId, version, locale) = (unit.Pass.Id, unit.Version.Value, unit.LocaleCode);
        return db.PrerenderUnits.Where(row =>
            row.PassId == passId && row.Version == version && row.Locale == locale);
    }

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
}
