using LoDb.Api.Modules.Admin.Prerender.Http.Views;
using LoDb.Api.Modules.Admin.Prerender.Http.Views.Progress;
using LoDb.Infrastructure.Persistence.Prerender;

namespace LoDb.Api.Modules.Admin.Prerender.Http.Monitor;

/// <summary>
/// The versions of a pass as its matrix draws them, a cell per locale and their sums: a pure
/// function of their units and of the pass's state.
/// </summary>
internal static class VersionRows
{
    /// <summary>
    /// A row per version of <paramref name="units"/>, in the order the worker reaches them
    /// (their lowest priority); a cell per code of <paramref name="locales"/>.
    /// </summary>
    public static IReadOnlyList<PrerenderVersionProgress> Of(
        IEnumerable<PrerenderUnit> units,
        IReadOnlyList<string> locales,
        PrerenderPassState passState)
    {
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(locales);
        return
        [
            .. units.GroupBy(static unit => unit.Version, StringComparer.Ordinal)
                .Select(version => RowOf([.. version], locales, passState))
                .OrderBy(static row => row.Rank)
                .ThenBy(static row => row.Version, StringComparer.Ordinal),
        ];
    }

    /// <summary>The code of one unit's cell in a pass in <paramref name="passState"/>.</summary>
    public static char CellOf(PrerenderUnit unit, PrerenderPassState passState)
    {
        ArgumentNullException.ThrowIfNull(unit);
        return unit.State switch
        {
            PrerenderUnitState.Pending => UnitCells.Pending,
            PrerenderUnitState.Completed when unit.PagesFailed > 0 =>
                UnitCells.CompletedWithFailures,
            PrerenderUnitState.Completed => UnitCells.Completed,
            PrerenderUnitState.Failed => UnitCells.Failed,
            _ => RunningCellOf(passState),
        };
    }

    // A unit left running: the pause moves the pass alone, a cancel or an end leaves it there.
    private static char RunningCellOf(PrerenderPassState passState) => passState switch
    {
        PrerenderPassState.Running => UnitCells.Running,
        PrerenderPassState.Paused => UnitCells.Stopped,
        _ => UnitCells.Interrupted,
    };

    private static PrerenderVersionProgress RowOf(
        List<PrerenderUnit> units,
        IReadOnlyList<string> locales,
        PrerenderPassState passState)
    {
        var byLocale = units.ToDictionary(static unit => unit.Locale, StringComparer.Ordinal);
        var cells = locales.Select(locale => byLocale.TryGetValue(locale, out var unit)
            ? CellOf(unit, passState)
            : UnitCells.NoUnit);
        return new PrerenderVersionProgress
        {
            Version = units[0].Version,
            Rank = units.Min(static unit => unit.Priority),
            Cells = string.Concat(cells),
            PagesTotal = units.Sum(static unit => (long)unit.PagesTotal),
            PagesWritten = units.Sum(static unit => (long)unit.PagesWritten),
            PagesSkipped = units.Sum(static unit => (long)unit.PagesSkipped),
            PagesFailed = units.Sum(static unit => (long)unit.PagesFailed),
            BytesWritten = units.Sum(static unit => unit.BytesWritten),
            UpdatedAt = units.Max(static unit => unit.UpdatedAt),
        };
    }
}
