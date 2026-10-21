namespace LoDb.Infrastructure.Persistence.Prerender;

/// <summary>
/// A row of <c>prerender_unit</c>: the pages of one (version, language) in a pass, with
/// where the worker stopped, so that a pass resumes across deploys.
/// </summary>
public sealed class PrerenderUnit
{
    /// <summary>The longest language tag, such as <c>zh-Hans</c>.</summary>
    public const int LocaleMaxLength = 16;

    /// <summary>The longest game version, as in <c>ddragon_version</c>.</summary>
    public const int VersionMaxLength = 32;

    /// <summary>The longest reason of a failure kept; a longer one is cut.</summary>
    public const int FailureMaxLength = 300;

    public long PassId { get; set; }

    public required string Version { get; set; }

    public required string Locale { get; set; }

    public required PrerenderUnitState State { get; set; }

    /// <summary>Lower goes first; the operator's version order becomes this.</summary>
    public int Priority { get; set; }

    public int PagesTotal { get; set; }

    public int PagesWritten { get; set; }

    public int PagesSkipped { get; set; }

    public int PagesFailed { get; set; }

    public long BytesWritten { get; set; }

    /// <summary>Index in the version's ordered page list of the next page to render.</summary>
    public int Cursor { get; set; }

    /// <summary>
    /// Rewritten at each page: never indexed, so that the table keeps its in-place updates.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>When the worker first started it; a resumption keeps it. Null while pending.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// When it ended, completed or failed, written once: what the admin reads units by in the
    /// order they ended. Null while it has not, and for a unit that ended before the column.
    /// </summary>
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>Why its last failed page failed, or why it failed; null when nothing did.</summary>
    public string? Failure { get; set; }

    /// <summary>
    /// The pages it should list, from a unit of the same version over the same types of page
    /// that completed before; null when none did.
    /// </summary>
    public int? PagesExpected { get; set; }

    public PrerenderPass? Pass { get; set; }
}
