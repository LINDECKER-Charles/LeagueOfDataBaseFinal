using LoDb.Api.Modules.Catalog.Reading;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Catalog.WarmUp;

/// <summary>
/// A warm-up: <c>/api/catalog/{version}/{lang}/warm-up?resources=champions&amp;resources=…</c>.
/// </summary>
/// <param name="Version">Patch version, such as 16.19.1.</param>
/// <param name="Lang">Data Dragon language, such as en_US.</param>
/// <param name="Resources">
/// The lists whose images to fetch: champions, items, runes, summoners. None prepares the
/// datasets alone, what a page without a list needs.
/// </param>
/// <param name="WarmUp">Runs the warm-up.</param>
/// <param name="Context">The call's, whose answer the proxy must not buffer.</param>
/// <param name="CancellationToken">The request's: the stream stops, the ingestion goes on.</param>
internal sealed record WarmUpRequest(
    [FromRoute(Name = "version")] string Version,
    [FromRoute(Name = "lang")] string Lang,
    [FromQuery(Name = "resources")] string[]? Resources,
    [FromServices] CatalogWarmUp WarmUp,
    HttpContext Context,
    CancellationToken CancellationToken)
{
    public CatalogScope Scope => new(Version, Lang);
}
