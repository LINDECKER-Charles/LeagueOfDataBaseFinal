using LoDb.Api.Modules.Catalog.Http;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Builds.Sharing;

/// <summary>The query of a shared build: <c>?version=&amp;lang=</c>, both optional.</summary>
/// <param name="Version">
/// The version the visitor browses, which the build's patch is compared with; the latest
/// when unset.
/// </param>
/// <param name="Lang">
/// The Data Dragon language of the names; the build's own language when unset.
/// </param>
/// <param name="Context">The call.</param>
internal sealed record ShareQuery(
    [FromQuery(Name = "version")] string? Version,
    [FromQuery(Name = "lang")] string? Lang,
    HttpContext Context)
{
    /// <summary>The 400 of a malformed version or language; null when both are usable.</summary>
    public CatalogProblem? Malformed =>
        Version is not null && !PatchVersion.TryParse(Version, out _)
            ? CatalogProblem.InvalidVersion(Version)
            : Lang is not null && !DdragonLanguage.TryParse(Lang, out _)
                ? CatalogProblem.InvalidLanguage(Lang)
                : null;

    /// <summary>The language of the names: the one asked, else the build's, else en_US.</summary>
    public string LanguageFor(string buildLanguage) =>
        Lang ?? (DdragonLanguage.TryParse(buildLanguage, out var own) ? own : DdragonLanguage.EnUs)
            .Code;
}
