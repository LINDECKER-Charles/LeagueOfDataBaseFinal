namespace LoDb.Infrastructure.Persistence.Prerender;

/// <summary>
/// Where a pass of the pre-rendering is, stored as <c>running</c>, <c>paused</c>,
/// <c>completed</c> or <c>cancelled</c>. The first two are the active states.
/// </summary>
public enum PrerenderPassState
{
    /// <summary>The worker takes the pass's units one after the other.</summary>
    Running,

    /// <summary>An operator stopped it; it resumes where it was.</summary>
    Paused,

    /// <summary>Every unit is over.</summary>
    Completed,

    /// <summary>An operator ended it; its units are left as they were.</summary>
    Cancelled,
}
