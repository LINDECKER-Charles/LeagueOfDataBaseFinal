using LoDb.Api.Modules.Admin.Prerender.Coverage;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Domain.Versions;

namespace LoDb.Api.Modules.Admin.Prerender.Passes.Planning;

/// <summary>
/// Plans a pass against the older patches and, when it skips what is complete, its epoch's
/// coverage: apart from its opening (<see cref="PassLauncher"/>), so that a pass with nothing
/// to render is refused before its row is written.
/// </summary>
/// <remarks>
/// A (version, locale) is complete for a pass when the epoch holds every type the pass
/// renders: a locale whose items are held is left out of a pass over the items, and rendered
/// again by a pass over every type. Each unit gets the pages a former unit of its version over
/// the same types listed (<see cref="ExpectedPages"/>), for the estimate of what is left.
/// </remarks>
internal sealed class PassPlanning(
    IPrerenderCatalog catalog,
    EpochCoverage coverage,
    ExpectedPages expected)
{
    private static readonly IReadOnlySet<(string Version, string Locale)> NoneHeld =
        new HashSet<(string Version, string Locale)>();

    public async Task<PassPlan> PlanAsync(PassLaunch launch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(launch);
        var scope = launch.Scope;
        var pinned = await catalog.PinnedVersionsAsync(cancellationToken);
        var held = scope.SkipComplete
            ? (await coverage.ReadAsync(launch.Epoch, cancellationToken)).HeldFor(scope.PageTypes)
            : NoneHeld;
        var units = UnitPlanner.Plan(pinned, scope, held);
        var skipped = held.Count == 0 ? 0 : UnitPlanner.Plan(pinned, scope).Count - units.Count;
        return new PassPlan(
            await WithExpectedAsync(units, scope, cancellationToken),
            skipped,
            IgnoredOf(scope, pinned));
    }

    private async Task<IReadOnlyList<PlannedUnit>> WithExpectedAsync(
        IReadOnlyList<PlannedUnit> units,
        PassScope scope,
        CancellationToken cancellationToken)
    {
        if (units.Count == 0)
        {
            return units;
        }

        var versions = units.Select(static unit => unit.Version.Value).Distinct().ToList();
        var pages = await expected.ReadAsync(TypeMask.Of(scope), versions, cancellationToken);
        return
        [
            .. units.Select(unit => pages.TryGetValue(unit.Version.Value, out var count)
                ? unit with { PagesExpected = count }
                : unit),
        ];
    }

    private static List<string> IgnoredOf(PassScope scope, IReadOnlyList<PatchVersion> pinned) =>
        [.. (scope.Versions ?? []).Except(pinned).Select(static version => version.Value)];
}
