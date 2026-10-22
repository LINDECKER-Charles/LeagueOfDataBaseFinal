using LoDb.Api.Modules.Catalog.Reading;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Catalog.Changes;

/// <summary>A patch's page of changes: <c>/api/catalog/{version}/{lang}/changes</c>.</summary>
/// <param name="Version">Patch version, such as 16.19.1.</param>
/// <param name="Lang">Data Dragon language, such as en_US.</param>
/// <param name="Changes">Reads the index and the catalogs that name its entities.</param>
/// <param name="Context">The call's, whose connection tells who asks.</param>
/// <param name="CancellationToken">The request's.</param>
internal sealed record ChangesRequest(
    [FromRoute(Name = "version")] string Version,
    [FromRoute(Name = "lang")] string Lang,
    [FromServices] PatchChangesGateway Changes,
    HttpContext Context,
    CancellationToken CancellationToken)
{
    public CatalogScope Scope => new(Version, Lang) { Caller = ColdCallers.Of(Context) };
}
