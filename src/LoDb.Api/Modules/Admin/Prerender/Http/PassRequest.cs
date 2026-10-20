namespace LoDb.Api.Modules.Admin.Prerender.Http;

/// <summary>
/// A pass to launch: the epoch it writes and its perimeter, every field optional. Empty, it
/// fills the served epoch with every page of every older patch, newest first.
/// </summary>
internal sealed record PassRequest
{
    /// <summary>
    /// The epoch written: the served one (the default) to fill it, a newer one to replace it.
    /// An older one is retired and refused.
    /// </summary>
    public int? Epoch { get; init; }

    /// <summary>The oldest version rendered, included (<c>14.1.1</c>).</summary>
    public string? Oldest { get; init; }

    /// <summary>The newest version rendered, included.</summary>
    public string? Newest { get; init; }

    /// <summary>The one type of page rendered: champions, items, runes or summoners.</summary>
    public string? PageType { get; init; }

    /// <summary>The one locale rendered, as in the URLs (fr, zh-Hans…).</summary>
    public string? Locale { get; init; }

    /// <summary>
    /// The versions to render first, in this order: the most asked for, which the operator
    /// reads from the edge's log. At most 100.
    /// </summary>
    public IReadOnlyList<string>? Priority { get; init; }
}
