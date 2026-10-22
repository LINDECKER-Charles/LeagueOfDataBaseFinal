using LoDb.Api.Modules.Admin.Prerender.Demand.Validation;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Queue;

/// <summary>
/// Adds one read of the SSR to <c>prerender_demand</c>, in one statement: a new page enters
/// pending (skipped when a pass already stored it in the served epoch); a known one gets the
/// increase of its count, whatever its state.
/// </summary>
/// <remarks>
/// The increase is the SSR's cumulative count less the last one kept, under the same SSR
/// instance; its whole count when the instance changed (a restart) or the count went down (the
/// path was evicted, then refused again). A read made twice, a second reader or a restart of
/// the API adds nothing twice.
/// </remarks>
internal sealed class DemandUpserts(IDbContextFactory<LoDbDbContext> contexts, TimeProvider clock)
{
    private const string Sql = """
        INSERT INTO prerender_demand AS d (path, version, locale, page_type, demand,
            ssr_instance, ssr_count, first_seen_at, last_seen_at, state, attempts, epoch,
            updated_at, reason)
        SELECT n.path, n.version, n.locale, n.page_type, n.count, @instance, n.count, @now, @now,
            n.state, 0, n.epoch, @now, n.reason
        FROM unnest(@paths, @versions, @locales, @types, @counts, @states, @epochs, @reasons)
            AS n(path, version, locale, page_type, count, state, epoch, reason)
        ON CONFLICT (path) DO UPDATE SET
            demand = d.demand + CASE
                WHEN d.ssr_instance = EXCLUDED.ssr_instance AND EXCLUDED.ssr_count >= d.ssr_count
                    THEN EXCLUDED.ssr_count - d.ssr_count
                ELSE EXCLUDED.ssr_count END,
            last_seen_at = CASE
                WHEN d.ssr_instance = EXCLUDED.ssr_instance AND EXCLUDED.ssr_count = d.ssr_count
                    THEN d.last_seen_at
                ELSE EXCLUDED.last_seen_at END,
            ssr_instance = EXCLUDED.ssr_instance,
            ssr_count = EXCLUDED.ssr_count
        """;

    /// <summary>
    /// Adds <paramref name="pages"/>, read from the SSR <paramref name="instance"/>, for the
    /// served <paramref name="epoch"/>.
    /// </summary>
    public async Task UpsertAsync(
        string instance,
        int epoch,
        IReadOnlyList<CountedPage> pages,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pages);
        if (pages.Count == 0)
        {
            return;
        }

        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var versions = pages.Select(static page => page.Page.Version.Value).Distinct().ToList();
        var served = await ServedUnits.ReadAsync(db, epoch, versions, cancellationToken);
        var stored = pages.Select(page => served.Holds(page.Page, page.Index)).ToArray();
        await db.Database.ExecuteSqlRawAsync(
            Sql,
            Parameters(instance, epoch, pages, stored),
            cancellationToken);
    }

    private NpgsqlParameter[] Parameters(
        string instance,
        int epoch,
        IReadOnlyList<CountedPage> pages,
        bool[] stored)
    {
        var skipped = PrerenderDemandColumns.ToText(PrerenderDemandState.Skipped);
        var pending = PrerenderDemandColumns.ToText(PrerenderDemandState.Pending);
        return
        [
            new("instance", instance),
            new("now", clock.GetUtcNow()),
            Texts("paths", pages.Select(static page => page.Page.Path)),
            Texts("versions", pages.Select(static page => page.Page.Version.Value)),
            Texts("locales", pages.Select(static page => page.Page.LocaleCode)),
            Texts("types", pages.Select(static page => page.Page.TypeName)),
            new("counts", NpgsqlDbType.Array | NpgsqlDbType.Bigint)
            {
                Value = pages.Select(static page => page.Count).ToArray(),
            },
            Texts("states", stored.Select(isStored => isStored ? skipped : pending)),
            new("epochs", NpgsqlDbType.Array | NpgsqlDbType.Integer)
            {
                Value = stored.Select(isStored => isStored ? epoch : (int?)null).ToArray(),
            },
            Texts(
                "reasons",
                stored.Select(static isStored => isStored ? ServedUnits.AlreadyStored : null)),
        ];
    }

    private static NpgsqlParameter Texts(string name, IEnumerable<string?> values) =>
        new(name, NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = values.ToArray() };
}
