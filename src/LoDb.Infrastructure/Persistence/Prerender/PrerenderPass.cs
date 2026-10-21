namespace LoDb.Infrastructure.Persistence.Prerender;

/// <summary>
/// A row of <c>prerender_pass</c>: one run that writes the pages of a perimeter into the page
/// store, under one epoch. At most one pass is active (running or paused) at a time.
/// </summary>
public sealed class PrerenderPass
{
    /// <summary>The longest reason of a pause kept; a longer one is cut.</summary>
    public const int PauseReasonMaxLength = 300;

    public long Id { get; set; }

    /// <summary>
    /// The epoch the pass writes. Epoch 0 is reserved for the store probes, so it is positive.
    /// </summary>
    public int Epoch { get; set; }

    /// <summary>
    /// Required because the default value, <see cref="PrerenderPassState.Running"/>, is active:
    /// a pass built without one would silently take the single active slot.
    /// </summary>
    public required PrerenderPassState State { get; set; }

    /// <summary>
    /// The perimeter as a JSON object (versions, locales, types of page); empty means all of it.
    /// Kept as text so that the engine owns its shape.
    /// </summary>
    public string Scope { get; set; } = "{}";

    /// <summary>
    /// The admin who launched the pass; none for an automatic one. An id and not an email, so
    /// that the table holds no personal data and outlives the account.
    /// </summary>
    public int? AuthorId { get; set; }

    /// <summary>
    /// Whether the pass is active, computed by the database from <see cref="State"/>: the
    /// column the unique index on active passes keys on.
    /// </summary>
    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>
    /// Since when it is paused; null otherwise. No constraint ties it to <see cref="State"/>:
    /// the stack before the column pauses passes without it.
    /// </summary>
    public DateTimeOffset? PausedAt { get; set; }

    /// <summary>
    /// Why the pass paused itself (the store or the SSR refusing it); null for a pause of an
    /// administrator, and once it runs again.
    /// </summary>
    public string? PauseReason { get; set; }

    /// <summary>
    /// When the pass last entered <see cref="PrerenderPassState.Running"/>; set exactly while it
    /// runs. With <see cref="ActiveSeconds"/>, the admin's rate leaves the paused time out.
    /// </summary>
    public DateTimeOffset? RunningSince { get; set; }

    /// <summary>
    /// Running time accrued before <see cref="RunningSince"/>: each pause, cancel or end adds
    /// the stretch that closes, so the pass's rate is pages over this plus the open stretch.
    /// </summary>
    public long ActiveSeconds { get; set; }

    // The counters below are a cache of the sums over the pass's units, which stay the
    // authority (the per-epoch volume is read from them). The engine updates both in the same
    // transaction, so the admin reads the pass row alone.

    public long PagesTotal { get; set; }

    public long PagesWritten { get; set; }

    public long PagesSkipped { get; set; }

    public long PagesFailed { get; set; }

    /// <summary>Bytes written to the store, kept here because the writer key cannot list.</summary>
    public long BytesWritten { get; set; }

    public List<PrerenderUnit> Units { get; set; } = [];
}
