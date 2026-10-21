using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Passes.Planning;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LoDb.Api.Modules.Admin.Prerender.Passes;

/// <summary>
/// Opens a pass: its row, running at once, and the units of its plan
/// (<see cref="PassPlanning"/>), in the order the worker takes them.
/// </summary>
/// <remarks>
/// At most one pass is active, running or paused: the database refuses a second one
/// (<c>ux_prerender_pass_active</c>), whoever launches it, at the same time or not. A unit's
/// pages are listed when the worker reaches it, so the pass's page count grows as it goes.
/// </remarks>
internal sealed class PassLauncher(IDbContextFactory<LoDbDbContext> contexts, TimeProvider clock)
{
    /// <summary>The index that keeps a second pass from being active.</summary>
    public const string ActivePassIndex = "ux_prerender_pass_active";

    /// <summary>
    /// Opens a pass over <paramref name="launch"/> with the units of <paramref name="plan"/>;
    /// null when one is already active.
    /// </summary>
    public async Task<long?> StartAsync(
        PassLaunch launch,
        PassPlan plan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentOutOfRangeException.ThrowIfLessThan(launch.Epoch, 1);
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        if (await db.PrerenderPasses.AnyAsync(static pass => pass.IsActive, cancellationToken))
        {
            return null;
        }

        var pass = NewPass(launch, plan.Units);
        db.PrerenderPasses.Add(pass);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsActivePassTaken(exception))
        {
            return null;
        }

        return pass.Id;
    }

    /// <summary>Whether a write failed because another pass is active.</summary>
    public static bool IsActivePassTaken(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: ActivePassIndex,
        };

    private PrerenderPass NewPass(PassLaunch launch, IReadOnlyList<PlannedUnit> units)
    {
        var now = clock.GetUtcNow();
        return new PrerenderPass
        {
            Epoch = launch.Epoch,
            State = PrerenderPassState.Running,
            Scope = PassScopeJson.Write(launch.Scope),
            AuthorId = launch.AuthorId,
            CreatedAt = now,
            RunningSince = now,
            Units = [.. units.Select(unit => RowOf(unit, now))],
        };
    }

    /// <summary>The row of a unit not started yet.</summary>
    public static PrerenderUnit RowOf(PlannedUnit unit, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(unit);
        return new PrerenderUnit
        {
            Version = unit.Version.Value,
            Locale = UiLocales.Code(unit.Locale),
            State = PrerenderUnitState.Pending,
            Priority = unit.Priority,
            PagesExpected = unit.PagesExpected,
            UpdatedAt = now,
        };
    }
}
