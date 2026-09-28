using LoDb.Api.Modules.Catalog.Reading;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Catalog.Search;

/// <summary>A search call: <c>/api/catalog/{version}/{lang}/search?q=&amp;types=</c>.</summary>
/// <param name="Version">Patch version, such as 16.19.1.</param>
/// <param name="Lang">Data Dragon language, such as en_US.</param>
/// <param name="Q">The text looked for, from 2 to 50 characters once trimmed.</param>
/// <param name="Types">
/// The resources searched, as their path segment (champions, items, runes, summoners),
/// repeated or comma-separated; all of them when absent.
/// </param>
/// <param name="Gateway">Opens the catalog.</param>
/// <param name="CancellationToken">The request's.</param>
internal sealed record SearchRequest(
    [FromRoute(Name = "version")] string Version,
    [FromRoute(Name = "lang")] string Lang,
    [FromQuery(Name = "q")] string? Q,
    [FromQuery(Name = "types")] string[]? Types,
    [FromServices] CatalogGateway Gateway,
    CancellationToken CancellationToken)
{
    public CatalogScope Scope => new(Version, Lang);
}
