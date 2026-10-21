using System.Net;

namespace LoDb.Api.Modules.Admin.Prerender.Rendering;

/// <summary>What the SSR answered for one page: status, headers and body, as sent.</summary>
internal sealed record RenderedPage
{
    public required HttpStatusCode Status { get; init; }

    /// <summary>The body as received: gzip for a page of the pass, never decompressed.</summary>
    public ReadOnlyMemory<byte> Body { get; init; }

    /// <summary>
    /// Every header of the answer, its content's included, by case-insensitive name; a header
    /// sent several times is one value joined by commas, as HTTP reads it.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Headers { get; init; }

    /// <summary>How long the SSR asks to wait before the next try, when it says.</summary>
    public TimeSpan? RetryAfter { get; init; }

    public string? Header(string name) => Headers.GetValueOrDefault(name);
}
