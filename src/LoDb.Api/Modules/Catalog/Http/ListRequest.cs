using LoDb.Api.Modules.Catalog.Reading;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Catalog.Http;

/// <summary>
/// A list call: <c>/api/catalog/{version}/{lang}/{type}?page=&amp;size=</c>.
/// </summary>
/// <param name="Version">Patch version, such as 16.19.1.</param>
/// <param name="Lang">Data Dragon language, such as en_US.</param>
/// <param name="Page">One-based page; with neither page nor size, the whole list.</param>
/// <param name="Size">Entries per page, 1 to 200; 50 when only the page is given.</param>
/// <param name="Gateway">Opens the catalog.</param>
/// <param name="CancellationToken">The request's.</param>
internal sealed record ListRequest(
    [FromRoute(Name = "version")] string Version,
    [FromRoute(Name = "lang")] string Lang,
    [FromQuery(Name = "page")] int? Page,
    [FromQuery(Name = "size")] int? Size,
    [FromServices] CatalogGateway Gateway,
    CancellationToken CancellationToken)
{
    public CatalogScope Scope => new(Version, Lang);
}
