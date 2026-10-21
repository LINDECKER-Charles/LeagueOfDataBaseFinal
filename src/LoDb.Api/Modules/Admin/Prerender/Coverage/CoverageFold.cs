using LoDb.Api.Modules.Admin.Prerender.Http.Views;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Infrastructure.Persistence.Prerender;

namespace LoDb.Api.Modules.Admin.Prerender.Coverage;

/// <summary>
/// Folds the units of an epoch's passes into one cell per (version, locale): a pure function
/// of the units and of the types each of their passes renders.
/// </summary>
/// <remarks>
/// <para>
/// Each type of page is decided by the latest completed unit whose pass renders it: held when
/// it failed no page. A unit cannot tell on which type its pages failed, so its failures mark
/// all its types: the retry renders them again. A clean pass over the items alone thus leaves
/// the failures a former pass left on the champions where they are.
/// </para>
/// <para>
/// Pages and bytes are the most any unit wrote, as the store census counts them: a unit that
/// renders the same pages again replaces them.
/// </para>
/// </remarks>
internal static class CoverageFold
{
    /// <summary>The cells of <paramref name="units"/>.</summary>
    /// <param name="units">The units of the epoch's passes, in any order.</param>
    /// <param name="typesByPass">The types each pass renders (<see cref="TypeMask"/>).</param>
    public static CoverageMap Of(
        IEnumerable<CoverageUnit> units,
        IReadOnlyDictionary<long, int> typesByPass)
    {
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(typesByPass);
        var cells = new Dictionary<(string Version, string Locale), CoverageCell>();
        foreach (var group in units.GroupBy(static unit => (unit.Version, unit.Locale)))
        {
            var ordered = group.OrderBy(static unit => unit.Order).ToList();
            if (CellOf(ordered, typesByPass) is { } cell)
            {
                cells[group.Key] = cell;
            }
        }

        return new CoverageMap(cells);
    }

    // The units of one (version, locale), the one that ended first first.
    private static CoverageCell? CellOf(
        List<CoverageUnit> units,
        IReadOnlyDictionary<long, int> typesByPass)
    {
        var deciders = TypeMask.Bits()
            .Select(bit => (Bit: bit, Unit: units.LastOrDefault(unit =>
                unit.State == PrerenderUnitState.Completed
                && TypeMask.Holds(typesByPass.GetValueOrDefault(unit.PassId, TypeMask.All), bit))))
            .Where(static decider => decider.Unit is not null)
            .ToList();
        var covered = deciders.Aggregate(TypeMask.None, static (mask, d) => mask | d.Bit);
        var held = deciders.Where(static decider => decider.Unit!.PagesFailed == 0)
            .Aggregate(TypeMask.None, static (mask, d) => mask | d.Bit);
        if (CodeOf(units, covered, held) is not { } code)
        {
            return null;
        }

        var ended = units.Where(static unit => unit.HasEnded).ToList();
        return new CoverageCell(
            code,
            held,
            units.Max(static unit => unit.PagesWritten),
            units.Max(static unit => unit.BytesWritten),
            deciders.Select(static decider => decider.Unit!)
                .Distinct()
                .Sum(static unit => unit.PagesFailed),
            ended.Count == 0 ? null : ended.Max(static unit => unit.Order));
    }

    private static char? CodeOf(List<CoverageUnit> units, int covered, int held)
    {
        if (covered == TypeMask.All)
        {
            return held == TypeMask.All ? UnitCells.Completed : UnitCells.CompletedWithFailures;
        }

        var lastEnded = units.LastOrDefault(static unit => unit.HasEnded);
        if (covered == TypeMask.None && lastEnded?.State == PrerenderUnitState.Failed)
        {
            return UnitCells.Failed;
        }

        var hasStarted = units.Exists(static unit =>
            unit.PagesWritten > 0 || unit.State == PrerenderUnitState.Running);
        return covered != TypeMask.None || hasStarted ? UnitCells.Partial : null;
    }
}
