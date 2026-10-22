using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace LoDb.Api.Modules.Admin.Prerender.Revision.Producing;

/// <summary>
/// Writes the pages to render again into <c>prerender_revision</c>, pending until their due
/// time, one statement per batch of rows.
/// </summary>
/// <remarks>
/// A page already in the queue for an epoch is due again, its tries forgotten, whatever it
/// became: only a new closure enqueues it again (the watermark sees to that), so its stored
/// copy is stale again. One the drain renders now is left to it.
/// </remarks>
internal static class RevisionRows
{
    /// <summary>Rows written per statement: some megabytes of parameters at most.</summary>
    public const int BatchSize = 10_000;

    private const string Sql = """
        INSERT INTO prerender_revision AS r (path, epoch, version, locale, state, attempts,
            enqueued_at, due_at, updated_at)
        SELECT n.path, n.epoch, n.version, n.locale, @pending, 0, @now, @due, @now
        FROM unnest(@paths, @epochs, @versions, @locales) AS n(path, epoch, version, locale)
        ON CONFLICT (path, epoch) DO UPDATE SET
            state = EXCLUDED.state,
            attempts = 0,
            enqueued_at = EXCLUDED.enqueued_at,
            due_at = EXCLUDED.due_at,
            written_at = NULL,
            updated_at = EXCLUDED.updated_at,
            reason = NULL
        WHERE r.state <> @rendering
        """;

    /// <summary>Writes the rows of <paramref name="enqueue"/>; the rows written.</summary>
    public static async Task<int> EnqueueAsync(
        LoDbDbContext db,
        RevisionEnqueue enqueue,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(enqueue);
        var rows = enqueue.Epochs.SelectMany(epoch =>
            enqueue.Pages.Select(page => (Page: page, Epoch: epoch)));
        var written = 0;
        foreach (var batch in rows.Chunk(BatchSize))
        {
            written += await db.Database.ExecuteSqlRawAsync(
                Sql,
                Parameters(batch, enqueue),
                cancellationToken);
        }

        return written;
    }

    private static NpgsqlParameter[] Parameters(
        (RevisionPage Page, int Epoch)[] batch,
        RevisionEnqueue enqueue) =>
    [
        new("pending", PrerenderDemandColumns.ToText(PrerenderDemandState.Pending)),
        new("rendering", PrerenderDemandColumns.ToText(PrerenderDemandState.Rendering)),
        new("now", enqueue.Now),
        new("due", enqueue.DueAt),
        Texts("paths", batch.Select(static row => row.Page.Path)),
        new("epochs", NpgsqlDbType.Array | NpgsqlDbType.Integer)
        {
            Value = batch.Select(static row => row.Epoch).ToArray(),
        },
        Texts("versions", batch.Select(static row => row.Page.Version)),
        Texts("locales", batch.Select(static row => row.Page.Locale)),
    ];

    private static NpgsqlParameter Texts(string name, IEnumerable<string> values) =>
        new(name, NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = values.ToArray() };
}
