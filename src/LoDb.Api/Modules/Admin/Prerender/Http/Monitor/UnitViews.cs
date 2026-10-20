using LoDb.Api.Modules.Admin.Prerender.Http.Views.Progress;
using LoDb.Infrastructure.Persistence.Prerender;

namespace LoDb.Api.Modules.Admin.Prerender.Http.Monitor;

/// <summary>The view of a unit where the worker left it: a pure function of its row.</summary>
internal static class UnitViews
{
    public static PrerenderUnitView Of(PrerenderUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        return new PrerenderUnitView
        {
            Version = unit.Version,
            Locale = unit.Locale,
            State = PrerenderColumns.ToText(unit.State),
            Cursor = unit.Cursor,
            PagesTotal = unit.PagesTotal,
            PagesWritten = unit.PagesWritten,
            PagesSkipped = unit.PagesSkipped,
            PagesFailed = unit.PagesFailed,
            BytesWritten = unit.BytesWritten,
            UpdatedAt = unit.UpdatedAt,
            // TODO(B2): StartedAt, EndedAt and Failure from the columns of the
            // PrerenderDiagnostics migration.
        };
    }

    /// <summary>Whether the unit ended, completed or failed.</summary>
    public static bool HasEnded(PrerenderUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        return unit.State is PrerenderUnitState.Completed or PrerenderUnitState.Failed;
    }

    /// <summary>Whether the unit failed, or completed with failed pages.</summary>
    public static bool HasFailures(PrerenderUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        return unit.State == PrerenderUnitState.Failed
            || (unit.State == PrerenderUnitState.Completed && unit.PagesFailed > 0);
    }
}
