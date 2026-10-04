using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Domain.Languages;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Builds.Http;

/// <summary>The query of a builds page: <c>?version=&amp;lang=</c>, both optional.</summary>
/// <param name="Version">
/// The version the visitor browses, such as 16.19.1; the latest when unset.
/// </param>
/// <param name="Lang">
/// The Data Dragon language of the names, such as fr_FR; en_US when unset.
/// </param>
internal sealed record BuildQuery(
    [FromQuery(Name = "version")] string? Version,
    [FromQuery(Name = "lang")] string? Lang)
{
    /// <summary>The browsing scope, its version still to be settled.</summary>
    public CatalogScope Browsing => new(Version, Lang ?? DdragonLanguage.EnUs.Code);
}
