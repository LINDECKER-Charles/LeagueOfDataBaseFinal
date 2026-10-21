namespace LoDb.Api.Modules.Admin.Prerender.Http;

/// <summary>
/// A pass to launch: the epoch it writes and its perimeter, as explicit lists. Every field is
/// optional: empty, it fills the served epoch with every page of every older patch, newest
/// first.
/// </summary>
internal sealed record PassRequest
{
    /// <summary>
    /// The epoch written: the served one by default, to fill it; a newer one to fill or to
    /// replace it. An older one is retired and refused.
    /// </summary>
    public int? Epoch { get; init; }

    /// <summary>
    /// The older patches rendered, as Data Dragon spells them; null for every one of them. A
    /// version that is not an older patch is left out and named back. At most 1,000.
    /// </summary>
    public IReadOnlyList<string>? Versions { get; init; }

    /// <summary>
    /// The locales rendered, as the URLs spell them (fr, zh-hans…); null for all 21.
    /// </summary>
    public IReadOnlyList<string>? Locales { get; init; }

    /// <summary>
    /// The types of page rendered (champions, items, runes, summoners); null for all four.
    /// </summary>
    public IReadOnlyList<string>? PageTypes { get; init; }

    /// <summary>
    /// The versions rendered first, in this order: the most asked for. At most 100.
    /// </summary>
    public IReadOnlyList<string>? Priority { get; init; }

    /// <summary>
    /// Whether each (version, locale) the epoch already holds for every type asked (the latest
    /// completed unit over each of them failed no page) is left out.
    /// </summary>
    public bool SkipComplete { get; init; }
}
