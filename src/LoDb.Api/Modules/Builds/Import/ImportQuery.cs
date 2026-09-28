using LoDb.Domain.Languages;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Builds.Import;

/// <summary>The query of an import: <c>?to=&amp;lang=</c>, both optional, and its call.</summary>
/// <param name="To">The patch to carry the build over to; the latest when unset.</param>
/// <param name="Lang">
/// The Data Dragon language the report names the dropped items in; en_US when unset.
/// </param>
/// <param name="Context">The call.</param>
internal sealed record ImportQuery(
    [FromQuery(Name = "to")] string? To,
    [FromQuery(Name = "lang")] string? Lang,
    HttpContext Context)
{
    public string Language => Lang ?? DdragonLanguage.EnUs.Code;
}
