using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Passes;
using LoDb.Api.Modules.Admin.Prerender.Telemetry;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace LoDb.Api.Modules.Admin.Prerender.Promotion;

/// <summary>
/// Gives a version that left the latest rank its units, one per locale: they join the active
/// pass ahead of its other units, or open a pass on the served epoch (ADR 0011).
/// </summary>
/// <remarks>
/// <para>
/// Idempotent, so that the promotion and its reconciliation may both ask: a locale already
/// covered gets nothing. A locale is covered by a unit of the active pass, or by a unit of
/// the served epoch that ended, completed or failed, provided its pass renders every type of
/// page. A unit left pending in a pass that ended is not: a promotion added it as the worker
/// completed the pass.
/// </para>
/// <para>
/// An active pass restricted to one type of page would leave the others out: the units wait
/// for it to end, and the reconciliation opens their pass then. Two writers at once (the
/// promotion and the reconciliation on two instances) meet on the database's keys: the second
/// one reads again what the first one wrote.
/// </para>
/// </remarks>
internal sealed class VersionEnqueuer(
    IDbContextFactory<LoDbDbContext> contexts,
    IOptions<PrerenderOptions> options,
    TimeProvider clock,
    ILogger<VersionEnqueuer> logger)
{
    private const string UnitKey = "pk_prerender_unit";

    public async Task<UnitEnqueue> EnsureAsync(PatchVersion version, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(version);
        UnitEnqueue result;
        try
        {
            result = await TryEnsureAsync(version, token);
        }
        catch (DbUpdateException exception) when (IsRace(exception))
        {
            result = await TryEnsureAsync(version, token);
        }

        Log(version, result);
        return result;
    }

    private static bool IsRace(DbUpdateException exception) =>
        PassLauncher.IsActivePassTaken(exception)
        || exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: UnitKey,
        };

    private async Task<UnitEnqueue> TryEnsureAsync(PatchVersion version, CancellationToken token)
    {
        await using var db = await contexts.CreateDbContextAsync(token);
        var missing = await MissingLocalesAsync(db, version, token);
        if (missing.Count == 0)
        {
            return UnitEnqueue.Nothing;
        }

        var active = await db.PrerenderPasses.SingleOrDefaultAsync(static p => p.IsActive, token);
        if (active is not null && !PassScopeJson.Read(active.Scope).HasEveryPageType)
        {
            return UnitEnqueue.Deferred(active.Id);
        }

        var pass = active ?? db.PrerenderPasses.Add(NewPass(version)).Entity;
        var first = active is null ? 0 : await FirstPriorityAsync(db, active.Id, token);
        var units = RowsOf(UnitPlanner.Ahead(version, missing, first));
        pass.Units.AddRange(units);
        await db.SaveChangesAsync(token);
        return UnitEnqueue.Added(pass.Id, units.Count);
    }

    private static async Task<int> FirstPriorityAsync(
        LoDbDbContext db,
        long passId,
        CancellationToken token) =>
        await db.PrerenderUnits.Where(unit => unit.PassId == passId)
            .MinAsync(static unit => (int?)unit.Priority, token) ?? 0;

    // No pass is active: one opens on the served epoch, for this version alone.
    private PrerenderPass NewPass(PatchVersion version)
    {
        var now = clock.GetUtcNow();
        return new PrerenderPass
        {
            Epoch = options.Value.ServedEpoch,
            State = PrerenderPassState.Running,
            Scope = PassScopeJson.Write(PassScope.Of(version)),
            CreatedAt = now,
            RunningSince = now,
        };
    }

    private async Task<List<UiLocale>> MissingLocalesAsync(
        LoDbDbContext db,
        PatchVersion version,
        CancellationToken token)
    {
        var rows = await db.PrerenderUnits.AsNoTracking()
            .Where(unit => unit.Version == version.Value)
            .Select(static unit => new Coverage
            {
                Locale = unit.Locale,
                State = unit.State,
                PassIsActive = unit.Pass!.IsActive,
                PassEpoch = unit.Pass.Epoch,
                ScopeJson = unit.Pass.Scope,
            })
            .ToListAsync(token);
        var served = options.Value.ServedEpoch;
        var codes = rows.Where(row => row.Covers(served))
            .Select(static row => row.Locale)
            .ToHashSet(StringComparer.Ordinal);
        return [.. UiLocales.All.Where(locale => !codes.Contains(UiLocales.Code(locale)))];
    }

    private List<PrerenderUnit> RowsOf(IReadOnlyList<PlannedUnit> planned)
    {
        var now = clock.GetUtcNow();
        return [.. planned.Select(unit => PassLauncher.RowOf(unit, now))];
    }

    private void Log(PatchVersion version, UnitEnqueue result)
    {
        if (result.IsDeferred)
        {
            PrerenderLog.PromotionDeferred(logger, version.Value, result.PassId!.Value);
        }
        else if (result is { Units: > 0, PassId: { } passId })
        {
            PrerenderLog.UnitsEnqueued(logger, result.Units, version.Value, passId);
        }
    }

    // A unit of the version, and the pass it belongs to.
    private sealed record Coverage
    {
        public required string Locale { get; init; }

        public PrerenderUnitState State { get; init; }

        public bool PassIsActive { get; init; }

        public int PassEpoch { get; init; }

        public required string ScopeJson { get; init; }

        public bool Covers(int servedEpoch)
        {
            var hasEnded = State is PrerenderUnitState.Completed or PrerenderUnitState.Failed;
            var isServed = PassEpoch == servedEpoch && hasEnded;
            return (PassIsActive || isServed) && PassScopeJson.Read(ScopeJson).HasEveryPageType;
        }
    }
}
