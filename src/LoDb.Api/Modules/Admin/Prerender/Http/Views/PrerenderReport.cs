namespace LoDb.Api.Modules.Admin.Prerender.Http.Views;

/// <summary>
/// The pre-rendering of the catalogue as the admin shows it: whether it may run, the epoch
/// nginx serves, the active pass, the history of the passes and what the store holds.
/// </summary>
internal sealed record PrerenderReport
{
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>
    /// Whether the pass and its store are both configured; until then no pass may start and the
    /// worker sleeps.
    /// </summary>
    public required bool IsConfigured { get; init; }

    /// <summary>Whether the patch that leaves the latest rank joins the pass by itself.</summary>
    public required bool EnqueuesPromotions { get; init; }

    /// <summary>
    /// The epoch nginx serves, from <c>LODB_ARCHIVE_SERVE_EPOCH</c>: it changes with that
    /// variable and a redeploy, never from the admin.
    /// </summary>
    public required int ServedEpoch { get; init; }

    /// <summary>
    /// A newer epoch ready to serve: set <c>LODB_ARCHIVE_SERVE_EPOCH</c> to it and redeploy to
    /// serve it. Null when none.
    /// </summary>
    public int? ReadyEpoch { get; init; }

    /// <summary>The active pass, running or paused; null when none.</summary>
    public PrerenderPassView? Current { get; init; }

    /// <summary>The latest 50 passes, newest first, the active one included.</summary>
    public required IReadOnlyList<PrerenderPassView> Passes { get; init; }

    public required PrerenderStorageView Storage { get; init; }

    /// <summary>
    /// The page store's bucket, a name and never a secret, for the retirement's variables;
    /// null while the store is not configured.
    /// </summary>
    public string? StoreBucket { get; init; }
}
