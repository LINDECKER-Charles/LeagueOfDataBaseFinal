namespace LoDb.Api.Modules.Admin.Prerender.Http.Views;

/// <summary>
/// The perimeter of a pass, spelled as the URLs spell it, sized for lists: the versions are
/// counted, not listed (the full list comes with a complete read of the pass's progress); a
/// list left null is not restricted.
/// </summary>
internal sealed record PrerenderScopeView
{
    /// <summary>How many older patches the pass lists; null when it takes every one.</summary>
    public int? VersionCount { get; init; }

    /// <summary>The newest version of the list; null when it takes every one.</summary>
    public string? NewestListed { get; init; }

    /// <summary>The oldest version of the list; null when it takes every one.</summary>
    public string? OldestListed { get; init; }

    /// <summary>The oldest version of a range, for a pass launched before the lists.</summary>
    public string? Oldest { get; init; }

    /// <summary>The newest version of a range, for a pass launched before the lists.</summary>
    public string? Newest { get; init; }

    /// <summary>The locales rendered, in the site's order; null for all 21.</summary>
    public IReadOnlyList<string>? Locales { get; init; }

    /// <summary>The types of page rendered; null for all four.</summary>
    public IReadOnlyList<string>? PageTypes { get; init; }

    /// <summary>
    /// <c>openers</c> when only the patch page and the openers of the index are rendered; null
    /// for every page.
    /// </summary>
    public string? Pages { get; init; }

    /// <summary>The versions rendered first, in this order (100 at most).</summary>
    public required IReadOnlyList<string> Priority { get; init; }

    /// <summary>Whether the units already held in the epoch were left out.</summary>
    public required bool SkipComplete { get; init; }
}
