using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Domain.Languages;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Profiles.Http;

/// <summary>The query of a profile read: <c>?version=&amp;lang=</c>, both optional.</summary>
/// <param name="Version">
/// The version the visitor browses, such as 16.19.1. A profile pinned to a version Data
/// Dragon still lists shows that one instead; the latest version when unset.
/// </param>
/// <param name="Lang">
/// The Data Dragon language of the names, such as fr_FR; en_US when unset.
/// </param>
internal sealed record ProfileQuery(
    [FromQuery(Name = "version")] string? Version,
    [FromQuery(Name = "lang")] string? Lang)
{
    /// <summary>The browsing scope, the version still to be settled against the pin.</summary>
    public CatalogScope Browsing => new(Version, Lang ?? DdragonLanguage.EnUs.Code);
}
