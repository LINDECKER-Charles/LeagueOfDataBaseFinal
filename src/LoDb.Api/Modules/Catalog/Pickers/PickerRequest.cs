using LoDb.Api.Modules.Catalog.Reading;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Catalog.Pickers;

/// <summary>A picker call: <c>/api/pickers/{type}?version=&amp;lang=</c>.</summary>
/// <param name="Version">Patch version, such as 16.19.1; required.</param>
/// <param name="Lang">Data Dragon language, such as en_US; required.</param>
/// <param name="Gateway">Opens the catalog.</param>
/// <param name="CancellationToken">The request's.</param>
/// <remarks>
/// Both are explicit, never a session's: the URL alone decides the answer, so that shared
/// caches may keep it. They bind as optional so that a missing one gets the typed 400 of a
/// malformed one, rather than the framework's untyped rejection.
/// </remarks>
internal sealed record PickerRequest(
    [FromQuery(Name = "version")] string? Version,
    [FromQuery(Name = "lang")] string? Lang,
    [FromServices] CatalogGateway Gateway,
    CancellationToken CancellationToken)
{
    public CatalogScope Scope => new(Version, Lang);
}
