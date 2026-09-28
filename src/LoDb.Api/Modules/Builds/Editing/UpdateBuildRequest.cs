using LoDb.Api.Modules.Builds.Editing.Bodies;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Builds.Editing;

/// <summary>The replacing build of <c>PUT /api/builds/{id}?lang=</c>, and its call.</summary>
/// <param name="Body">The submitted build.</param>
/// <param name="Lang">The language of the editor, in which a refusal names the items.</param>
/// <param name="Context">The call.</param>
internal sealed record UpdateBuildRequest(
    [FromBody] BuildRequest Body,
    [FromQuery(Name = "lang")] string? Lang,
    HttpContext Context);
