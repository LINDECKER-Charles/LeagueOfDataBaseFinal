using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;

namespace LoDb.Api.Modules.Admin.Prerender.Passes;

/// <summary>
/// The order in which a pass renders its units: one (version, locale) at a time, so that the
/// API loads each catalog once for all its pages; the versions the operator put first, then
/// the others newest first; within a version, the locales in the site's order.
/// </summary>
internal static class UnitPlanner
{
    /// <summary>The units of <paramref name="scope"/> among the pinned versions.</summary>
    /// <param name="pinned">The older patches, newest first.</param>
    /// <param name="scope">The perimeter of the pass.</param>
    public static IReadOnlyList<PlannedUnit> Plan(
        IReadOnlyList<PatchVersion> pinned,
        PassScope scope)
    {
        ArgumentNullException.ThrowIfNull(pinned);
        ArgumentNullException.ThrowIfNull(scope);
        var admitted = pinned.Where(scope.Admits).ToList();
        var first = scope.Priority.Where(admitted.Contains).Distinct().ToList();
        var versions = first.Concat(admitted.Except(first)).ToList();
        var locales = UiLocales.All.Where(scope.Admits).ToList();
        return Ranked(versions, locales, firstPriority: 0);
    }

    /// <summary>
    /// The units of <paramref name="version"/> in <paramref name="locales"/>, ranked before
    /// every unit whose priority is <paramref name="firstPriority"/> or more: a patch that just
    /// left the latest rank is the most asked for of the older ones.
    /// </summary>
    public static IReadOnlyList<PlannedUnit> Ahead(
        PatchVersion version,
        IReadOnlyList<UiLocale> locales,
        int firstPriority)
    {
        ArgumentNullException.ThrowIfNull(locales);
        return Ranked([version], locales, firstPriority - locales.Count);
    }

    private static List<PlannedUnit> Ranked(
        List<PatchVersion> versions,
        IReadOnlyList<UiLocale> locales,
        int firstPriority) =>
        [
            .. versions.SelectMany((version, rank) => locales.Select((locale, index) =>
                new PlannedUnit(version, locale, firstPriority + (rank * locales.Count) + index))),
        ];
}
