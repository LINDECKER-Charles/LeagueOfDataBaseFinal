using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;

namespace LoDb.Api.Modules.Analytics.Pages;

/// <summary>
/// A requested address that may show a counted page, as its path and query read, before the
/// catalog is asked anything: <c>/{locale}[/{resource}[/{entry}]][?lang=…&amp;version=…]</c>.
/// </summary>
internal sealed record PageAddress
{
    public required UiLocale Locale { get; init; }

    /// <summary>The resource of a list or a detail; null on the home page.</summary>
    public ResourceType? Resource { get; init; }

    /// <summary>The entity segment of a detail (<c>Ahri</c>, <c>1036-long-sword</c>).</summary>
    public string? Entry { get; init; }

    /// <summary>The first <c>lang</c> of the query, if any.</summary>
    public string? Lang { get; init; }

    /// <summary>The first <c>version</c> of the query, if any.</summary>
    public string? Version { get; init; }

    /// <summary>The page's path, one per page whatever the trailing slash.</summary>
    public required string Path { get; init; }
}
