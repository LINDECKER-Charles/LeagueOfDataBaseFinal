using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.States.Details;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Catalog.Http;

/// <summary>A detail call: <c>/api/catalog/{version}/{lang}/{type}/{id}</c>.</summary>
/// <param name="Version">Patch version, such as 16.19.1.</param>
/// <param name="Lang">Data Dragon language, such as en_US.</param>
/// <param name="Id">
/// The entry's id, or its canonical segment: "1036-long-sword" names item 1036; the slug is
/// decorative and never checked (ADR 0005).
/// </param>
/// <param name="Gateway">Opens the catalog.</param>
/// <param name="States">Reads where the page stands in the index of states.</param>
/// <param name="Context">The call's, whose connection tells who asks.</param>
/// <param name="CancellationToken">The request's.</param>
internal sealed record DetailRequest(
    [FromRoute(Name = "version")] string Version,
    [FromRoute(Name = "lang")] string Lang,
    [FromRoute(Name = "id")] string Id,
    [FromServices] CatalogGateway Gateway,
    [FromServices] StateGateway States,
    HttpContext Context,
    CancellationToken CancellationToken)
{
    public CatalogScope Scope => new(Version, Lang) { Caller = ColdCallers.Of(Context) };

    /// <summary>
    /// The state fields of the page of <paramref name="entity"/>: read for the page's caller,
    /// as the page's catalog was.
    /// </summary>
    public Task<StateRead> ReadStateAsync(DetailEntity entity) =>
        States.ReadAsync(entity, Scope.Caller, CancellationToken);
}
