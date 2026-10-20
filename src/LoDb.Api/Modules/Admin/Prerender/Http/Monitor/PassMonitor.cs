using LoDb.Api.Modules.Admin.Prerender.Http.History;
using LoDb.Api.Modules.Admin.Prerender.Http.Views.Progress;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Workers;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Admin.Prerender.Http.Monitor;

/// <summary>
/// Reads where a pass is, active or over, for the admin's live monitor: read every few
/// seconds, and only the versions changed since the previous read when it is recent.
/// </summary>
/// <remarks>
/// TODO(B2): ProgressQueries, the indexed reads of the spec (running, next, recent and
/// failures by <c>ended_at</c>, changed versions) in place of every unit of the pass; the
/// engine's pulse (EnginePulse, UnitWaits, SsrOutage), and the waiting versions left out of
/// <see cref="PrerenderPassProgress.Next"/>.
/// </remarks>
internal sealed class PassMonitor(
    LoDbDbContext db,
    PassHistory history,
    IConfiguration configuration,
    TimeProvider clock)
{
    /// <summary>The next units shown.</summary>
    public const int MaxNext = 5;

    /// <summary>The units that ended last shown.</summary>
    public const int MaxRecent = 12;

    /// <summary>The units with failures shown.</summary>
    public const int MaxFailures = 20;

    /// <summary>
    /// A <c>since</c> older than this reads every version again: the client was away, and a
    /// complete read now and then also brings the full list of the scope's versions.
    /// </summary>
    public static readonly TimeSpan CompleteReadAfter = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Where the pass <paramref name="id"/> is: every version when <paramref name="since"/> is
    /// null or old, else the versions with a unit changed after it. Null for no such pass.
    /// </summary>
    public async Task<PrerenderPassProgress?> ProgressAsync(
        long id,
        DateTimeOffset? since,
        CancellationToken cancellationToken)
    {
        var facts = await history.OneAsync(id, cancellationToken);
        if (facts is null)
        {
            return null;
        }

        var units = await db.PrerenderUnits.AsNoTracking()
            .Where(unit => unit.PassId == id)
            .ToListAsync(cancellationToken);
        return Assemble(facts, units, since);
    }

    /// <summary>
    /// The units of <paramref name="version"/> in the pass <paramref name="id"/>, in the site's
    /// order of the locales; null for no such pass.
    /// </summary>
    public async Task<PrerenderVersionUnits?> VersionAsync(
        long id,
        PatchVersion version,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (!await db.PrerenderPasses.AnyAsync(pass => pass.Id == id, cancellationToken))
        {
            return null;
        }

        var units = await db.PrerenderUnits.AsNoTracking()
            .Where(unit => unit.PassId == id && unit.Version == version.Value)
            .ToListAsync(cancellationToken);
        return new PrerenderVersionUnits
        {
            PassId = id,
            Version = version.Value,
            Units = [.. units.OrderBy(static unit => SiteRank(unit.Locale)).Select(UnitViews.Of)],
        };
    }

    private PrerenderPassProgress Assemble(
        PassFacts facts,
        List<PrerenderUnit> units,
        DateTimeOffset? since)
    {
        var now = clock.GetUtcNow();
        var isComplete = since is not { } from || from < now - CompleteReadAfter;
        var locales = LocalesOf(units);
        var shown = isComplete ? units : Changed(units, since!.Value);
        var failures = units.Where(UnitViews.HasFailures).ToList();
        return new PrerenderPassProgress
        {
            GeneratedAt = now,
            Pass = PassViews.Of(facts, now),
            Locales = locales,
            IsComplete = isComplete,
            Versions = VersionRows.Of(shown, locales, facts.Pass.State),
            ScopeVersions = isComplete ? ScopeVersionsOf(facts.Pass) : null,
            Running = [.. units.Where(IsRunning).Select(UnitViews.Of)],
            Next = NextOf(units),
            Recent = Latest(units.Where(UnitViews.HasEnded), MaxRecent),
            Failures = Latest(failures, MaxFailures),
            UnitsWithFailures = failures.Count,
            Pulse = new PrerenderEnginePulse
            {
                IsWorkerEnabled = ConventionalWorkers.IsEnabled(configuration),
                Waiting = [],
            },
        };
    }

    // Every unit of a version with one unit changed: its cells and sums are read whole.
    private static List<PrerenderUnit> Changed(List<PrerenderUnit> units, DateTimeOffset since)
    {
        var changed = units.Where(unit => unit.UpdatedAt > since)
            .Select(static unit => unit.Version)
            .ToHashSet(StringComparer.Ordinal);
        return [.. units.Where(unit => changed.Contains(unit.Version))];
    }

    private static List<string> LocalesOf(List<PrerenderUnit> units)
    {
        var codes = units.Select(static unit => unit.Locale).ToHashSet(StringComparer.Ordinal);
        return [.. UiLocales.All.Select(UiLocales.Code).Where(codes.Contains)];
    }

    private static List<string>? ScopeVersionsOf(PrerenderPass pass) =>
        PassScopeJson.Read(pass.Scope).Versions?.Select(static version => version.Value).ToList();

    private static bool IsRunning(PrerenderUnit unit) => unit.State == PrerenderUnitState.Running;

    // The order of UnitQueue.NextAsync.
    private static List<PrerenderUnitKey> NextOf(List<PrerenderUnit> units) =>
    [
        .. units.Where(static unit => unit.State == PrerenderUnitState.Pending)
            .OrderBy(static unit => unit.Priority)
            .ThenBy(static unit => unit.Version, StringComparer.Ordinal)
            .ThenBy(static unit => unit.Locale, StringComparer.Ordinal)
            .Take(MaxNext)
            .Select(static unit => new PrerenderUnitKey
            {
                Version = unit.Version,
                Locale = unit.Locale,
            }),
    ];

    // TODO(B2): newest first by ended_at; updated_at stands for it until the column exists.
    private static List<PrerenderUnitView> Latest(IEnumerable<PrerenderUnit> units, int count) =>
    [
        .. units.OrderByDescending(static unit => unit.UpdatedAt)
            .Take(count)
            .Select(UnitViews.Of),
    ];

    private static int SiteRank(string code) =>
        UiLocales.TryParse(code, out var locale) ? (int)locale : int.MaxValue;
}
