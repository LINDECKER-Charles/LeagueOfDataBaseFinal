namespace LoDb.Api.Modules.Admin.Prerender.Http.Views;

/// <summary>
/// The perimeter of a pass, spelled as the URLs spell it; a field left null is not restricted.
/// </summary>
internal sealed record PrerenderScopeView
{
    /// <summary>The oldest version rendered, included.</summary>
    public string? Oldest { get; init; }

    /// <summary>The newest version rendered, included.</summary>
    public string? Newest { get; init; }

    /// <summary>The one type of page rendered: champions, items, runes or summoners.</summary>
    public string? PageType { get; init; }

    /// <summary>The one locale rendered, as in the URLs (fr, zh-Hans…).</summary>
    public string? Locale { get; init; }

    /// <summary>The versions rendered first, in this order.</summary>
    public required IReadOnlyList<string> Priority { get; init; }
}
