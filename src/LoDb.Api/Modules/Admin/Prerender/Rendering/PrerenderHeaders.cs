using System.Collections.Frozen;

namespace LoDb.Api.Modules.Admin.Prerender.Rendering;

/// <summary>
/// The headers the pass and the SSR exchange (ADR 0011), as the SSR's
/// <c>server/prerender</c> and <c>server/admission</c> name them.
/// </summary>
internal static class PrerenderHeaders
{
    /// <summary>Marks a request of the pass; nginx clears it on public traffic.</summary>
    public const string Prerender = "X-LoDb-Prerender";

    public const string PrerenderValue = "1";

    /// <summary>Set by the SSR, on the pass's lane only, when the page may be stored.</summary>
    public const string CacheClass = "X-LoDb-Cache-Class";

    /// <summary>An older patch's page of a closed state: it never changes again.</summary>
    public const string ArchivedClass = "archived";

    /// <summary>
    /// An older patch's page of the open state, or of a state the index does not know yet: it
    /// changes once, when a promotion closes the state and the page is rendered again.
    /// </summary>
    public const string RevisableClass = "revisable";

    /// <summary>The classes the pass stores; any other mark is a page it skips.</summary>
    public static readonly FrozenSet<string> StoredClasses =
        FrozenSet.Create(StringComparer.Ordinal, ArchivedClass, RevisableClass);

    /// <summary>The digest of the build that rendered the page, on the pass's lane only.</summary>
    public const string Build = "X-LoDb-Build";

    /// <summary>The scheme the pages are rendered for, as the edge forwards it.</summary>
    public const string ForwardedProto = "X-Forwarded-Proto";

    public const string ForwardedHost = "X-Forwarded-Host";

    public const string Https = "https";

    /// <summary>
    /// The only encoding asked for: without it the SSR answers plain HTML, and the store keeps
    /// what it gets.
    /// </summary>
    public const string Gzip = "gzip";
}
