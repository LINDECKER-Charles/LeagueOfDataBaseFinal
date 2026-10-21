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
            StartedAt = unit.StartedAt,
            UpdatedAt = unit.UpdatedAt,
            EndedAt = unit.EndedAt,
            Failure = unit.Failure,
        };
    }
}
