using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using Microsoft.EntityFrameworkCore;
using QueueRow = LoDb.Infrastructure.Persistence.Prerender.Demand.PrerenderDemandPage;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Http;

/// <summary>
/// Lists the pages of the automatic queue in one state, <see cref="PageSize"/> at a time, in
/// the order the admin reads them (<see cref="PrerenderDemandPages.Pages"/>).
/// </summary>
internal sealed class DemandPagesReader(IDbContextFactory<LoDbDbContext> contexts)
{
    /// <summary>The pages of the queue on one page of the list.</summary>
    public const int PageSize = 50;

    /// <summary>The last page of the list that can be asked for: its rows fit an int.</summary>
    public const int MaxPage = int.MaxValue / PageSize;

    /// <summary>The <paramref name="page"/>-th page (from 1) of the pages in that state.</summary>
    public async Task<PrerenderDemandPages> ReadAsync(
        PrerenderDemandState state,
        int page,
        CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var rows = db.PrerenderDemand.AsNoTracking().Where(row => row.State == state);
        var listed = await Ordered(rows, state)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);
        return new PrerenderDemandPages
        {
            State = PrerenderDemandColumns.ToText(state),
            Page = page,
            PageSize = PageSize,
            Total = await rows.LongCountAsync(cancellationToken),
            Pages = [.. listed.Select(ListedOf)],
        };
    }

    // The path breaks the ties: a page never shows on two pages of the list.
    private static IQueryable<QueueRow> Ordered(
        IQueryable<QueueRow> rows,
        PrerenderDemandState state) => state switch
    {
        PrerenderDemandState.Pending => rows
            .OrderByDescending(static row => row.Demand)
            .ThenBy(static row => row.FirstSeenAt)
            .ThenBy(static row => row.Path),
        PrerenderDemandState.Done => rows
            .OrderByDescending(static row => row.RenderedAt)
            .ThenBy(static row => row.Path),
        _ => rows
            .OrderByDescending(static row => row.UpdatedAt)
            .ThenBy(static row => row.Path),
    };

    private static PrerenderDemandListedPage ListedOf(QueueRow row) => new()
    {
        Path = row.Path,
        Version = row.Version,
        Locale = row.Locale,
        PageType = row.PageType,
        State = PrerenderDemandColumns.ToText(row.State),
        Demand = row.Demand,
        Attempts = row.Attempts,
        FirstSeenAt = row.FirstSeenAt,
        LastSeenAt = row.LastSeenAt,
        RenderedAt = row.RenderedAt,
        Epoch = row.Epoch,
        Bytes = row.Bytes,
        StoredClass = row.StoredClass,
        SupersededAt = row.SupersededAt,
        Reason = row.Reason,
    };
}
