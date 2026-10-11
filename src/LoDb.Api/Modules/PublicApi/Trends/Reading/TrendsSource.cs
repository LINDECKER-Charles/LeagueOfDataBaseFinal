using LoDb.Infrastructure.Audience;
using LoDb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.PublicApi.Trends.Reading;

/// <summary>
/// The entity counters of each day, as <c>{"{type}:{id}": views}</c>: from the daily aggregates
/// of the in-house measure (<c>analytics_daily</c>, which go-api read from the files of the
/// storage volume) before <see cref="AudienceOptions.TrendsSwitchDate"/>, from Umami's figures
/// (<c>audience_entity_daily</c>) from that day on. Without a switch date, the aggregates alone.
/// </summary>
/// <remarks>
/// Umami's collection is public, so its figures can be inflated: an entity's day counts at
/// most <see cref="AudienceOptions.ClipFactor"/> times the median of its days within the
/// <see cref="AudienceOptions.ClipWindowDays"/> that end with it (the lower middle of an even
/// count). The bound follows a steady audience and caps a day that leaps above it; an entity's
/// first day is its own median.
/// </remarks>
internal sealed class TrendsSource(
    IDbContextFactory<LoDbDbContext> contexts,
    IOptions<AudienceOptions> audience)
{
    /// <summary>
    /// The <c>entities</c> map of each day from <paramref name="from"/> to
    /// <paramref name="to"/>, as JSON text; null for a day without one.
    /// </summary>
    public async Task<IReadOnlyList<string?>> ReadEntitiesAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var switchDate = audience.Value.TrendsSwitchDate;
        var lastAggregated = switchDate is { } first && first <= to ? first.AddDays(-1) : to;
        var firstPulled = switchDate is { } start && start > from ? start : from;
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var days = new List<string?>();
        if (from <= lastAggregated)
        {
            days.AddRange(await AggregatedAsync(db, from, lastAggregated, cancellationToken));
        }

        if (switchDate is not null && firstPulled <= to)
        {
            days.AddRange(await PulledAsync(db, firstPulled, to, cancellationToken));
        }

        return days;
    }

    private static Task<List<string?>> AggregatedAsync(
        LoDbDbContext db,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken) =>
        db.Database
            .SqlQuery<string?>(
                $"""
                SELECT (buckets -> 'entities')::text AS "Value"
                  FROM analytics_daily
                 WHERE day BETWEEN {from} AND {to}
                 ORDER BY day
                """)
            .ToListAsync(cancellationToken);

    // One map per day, each entity's views bounded by the median of its recent days.
    private Task<List<string?>> PulledAsync(
        LoDbDbContext db,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var factor = audience.Value.ClipFactor;
        var earlierDays = audience.Value.ClipWindowDays - 1;
        return db.Database
            .SqlQuery<string?>(
                $"""
                SELECT jsonb_object_agg(bounded.entity, bounded.views)::text AS "Value"
                FROM (
                  SELECT pulled.day, pulled.entity,
                         LEAST(pulled.views, ceil({factor} * recent.median)::bigint) AS views
                  FROM audience_entity_daily pulled
                  CROSS JOIN LATERAL (
                    SELECT percentile_disc(0.5) WITHIN GROUP (ORDER BY earlier.views) AS median
                    FROM audience_entity_daily earlier
                    WHERE earlier.entity = pulled.entity
                      AND earlier.day BETWEEN pulled.day - {earlierDays} AND pulled.day
                  ) recent
                  WHERE pulled.day BETWEEN {from} AND {to}
                ) bounded
                GROUP BY bounded.day
                ORDER BY bounded.day
                """)
            .ToListAsync(cancellationToken);
    }
}
