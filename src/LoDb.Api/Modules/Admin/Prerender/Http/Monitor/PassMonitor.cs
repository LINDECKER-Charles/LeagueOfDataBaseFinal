using LoDb.Api.Modules.Admin.Prerender.Http.History;
using LoDb.Api.Modules.Admin.Prerender.Http.Views.Progress;
using LoDb.Api.Modules.Admin.Prerender.Pages;
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
/// Each part is one indexed read (<see cref="ProgressQueries"/>); the engine's own signals
/// come from this instance's memory (<see cref="PulseReader"/>), and the versions it sees
/// waiting are left out of what comes next, as the worker leaves them for later.
/// </remarks>
internal sealed class PassMonitor(
    LoDbDbContext db,
    PassHistory history,
    ProgressQueries queries,
    PulseReader pulses,
    TimeProvider clock)
{
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
        return facts is null ? null : await AssembleAsync(facts, since, cancellationToken);
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

    private async Task<PrerenderPassProgress> AssembleAsync(
        PassFacts facts,
        DateTimeOffset? since,
        CancellationToken cancellationToken)
    {
        var (id, now) = (facts.Pass.Id, clock.GetUtcNow());
        var isComplete = since is not { } from || from < now - CompleteReadAfter;
        var pulse = pulses.Of(facts.Pass);
        var waiting = pulse.Waiting.Select(static wait => wait.Version).ToList();
        var locales = await queries.LocalesAsync(id, cancellationToken);
        var changedSince = isComplete ? null : since;
        var units = await queries.VersionUnitsAsync(id, changedSince, cancellationToken);
        return new PrerenderPassProgress
        {
            GeneratedAt = now,
            Pass = PassViews.Of(facts, now),
            Locales = locales,
            IsComplete = isComplete,
            Versions = VersionRows.Of(units, locales, facts.Pass.State),
            ScopeVersions = isComplete ? ScopeVersionsOf(facts.Pass) : null,
            Running = await queries.RunningAsync(id, cancellationToken),
            Next = await queries.NextAsync(id, waiting, cancellationToken),
            Recent = await queries.RecentAsync(id, cancellationToken),
            Failures = await queries.FailuresAsync(id, cancellationToken),
            UnitsWithFailures = await queries.FailureCountAsync(id, cancellationToken),
            Pulse = pulse,
        };
    }

    private static List<string>? ScopeVersionsOf(PrerenderPass pass) =>
        PassScopeJson.Read(pass.Scope).Versions?.Select(static version => version.Value).ToList();

    private static int SiteRank(string code) =>
        UiLocales.TryParse(code, out var locale) ? (int)locale : int.MaxValue;
}
