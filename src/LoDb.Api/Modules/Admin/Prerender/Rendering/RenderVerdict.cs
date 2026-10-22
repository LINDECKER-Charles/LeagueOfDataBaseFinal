namespace LoDb.Api.Modules.Admin.Prerender.Rendering;

/// <summary>What the pass does with a page the SSR answered.</summary>
internal enum RenderVerdict
{
    /// <summary>
    /// A 200 of a stored class (<c>archived</c> or <c>revisable</c>), gzip, with its build:
    /// written.
    /// </summary>
    Storable,

    /// <summary>
    /// A page that is not stored, for good: a redirect, a 404, a class the pass does not
    /// store (the latest patch's, a mark it does not know). Counted <c>skipped</c>.
    /// </summary>
    Skipped,

    /// <summary>The SSR had no room for it (503, 429): tried again after its Retry-After.</summary>
    Refused,

    /// <summary>
    /// A failure that may not last: a 5xx, a render missing part of itself (a 200 kept out of
    /// every cache), an answer the pass cannot store as it is. Tried again later, then failed.
    /// </summary>
    Transient,

    /// <summary>
    /// A 4xx but 404 and 410: the SSR refuses the pass itself (its public host is not allowed,
    /// say), so every page would fail alike. The pass pauses.
    /// </summary>
    Misconfigured,
}
