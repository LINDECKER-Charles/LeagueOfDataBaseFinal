using LoDb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Http;

/// <summary>
/// The last <see cref="Count"/> hours of the automatic queue, the current one last: the pages
/// that entered it and those it wrote, hour by hour in UTC, an hour without any at zero.
/// </summary>
/// <remarks>
/// Two grouped counts: the pages written on their partial index of <c>rendered_at</c>; the
/// pages entered over the table, bounded by the queue's own bounds.
/// </remarks>
internal static class DemandHours
{
    /// <summary>The hours the admin charts.</summary>
    public const int Count = 24;

    public static async Task<IReadOnlyList<PrerenderDemandHour>> ReadAsync(
        LoDbDbContext db,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        var first = HourOf(now).AddHours(1 - Count);
        var entered = await EnteredAsync(db, first, cancellationToken);
        var written = await WrittenAsync(db, first, cancellationToken);
        return
        [
            .. Enumerable.Range(0, Count).Select(hour => first.AddHours(hour)).Select(start =>
                new PrerenderDemandHour
                {
                    Start = start,
                    Entered = entered.GetValueOrDefault(start),
                    Written = written.GetValueOrDefault(start),
                }),
        ];
    }

    private static Task<Dictionary<DateTimeOffset, long>> EnteredAsync(
        LoDbDbContext db,
        DateTimeOffset first,
        CancellationToken cancellationToken) =>
        db.Database.SqlQuery<HourCount>(
                $"""
                SELECT date_trunc('hour', first_seen_at, 'UTC') AS "Hour", count(*) AS "Pages"
                  FROM prerender_demand
                 WHERE first_seen_at >= {first}
                 GROUP BY 1
                """)
            .ToDictionaryAsync(static row => row.Hour, static row => row.Pages, cancellationToken);

    private static Task<Dictionary<DateTimeOffset, long>> WrittenAsync(
        LoDbDbContext db,
        DateTimeOffset first,
        CancellationToken cancellationToken) =>
        db.Database.SqlQuery<HourCount>(
                $"""
                SELECT date_trunc('hour', rendered_at, 'UTC') AS "Hour", count(*) AS "Pages"
                  FROM prerender_demand
                 WHERE rendered_at IS NOT NULL AND rendered_at >= {first}
                 GROUP BY 1
                """)
            .ToDictionaryAsync(static row => row.Hour, static row => row.Pages, cancellationToken);

    private static DateTimeOffset HourOf(DateTimeOffset at)
    {
        var utc = at.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
    }

    // A row of the grouped counts, as the raw queries name their columns.
    private sealed class HourCount
    {
        public DateTimeOffset Hour { get; init; }

        public long Pages { get; init; }
    }
}
