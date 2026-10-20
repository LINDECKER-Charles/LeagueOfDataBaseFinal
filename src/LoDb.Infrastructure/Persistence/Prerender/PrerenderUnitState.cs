namespace LoDb.Infrastructure.Persistence.Prerender;

/// <summary>
/// Where the pages of one (version, language) are, stored as <c>pending</c>, <c>running</c>,
/// <c>completed</c> or <c>failed</c>.
/// </summary>
public enum PrerenderUnitState
{
    /// <summary>Not started.</summary>
    Pending,

    /// <summary>The worker is writing its pages, from the cursor on.</summary>
    Running,

    /// <summary>Every page was written, or skipped for good.</summary>
    Completed,

    /// <summary>The unit stopped on a failure; a later tick may take it again.</summary>
    Failed,
}
