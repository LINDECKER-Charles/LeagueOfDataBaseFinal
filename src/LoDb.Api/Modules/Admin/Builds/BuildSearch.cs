using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Builds;

/// <summary>The filters of the build list, both optional.</summary>
/// <param name="Text">Held by the name or the champion id, whatever its case.</param>
/// <param name="Visibility">public or private; both when absent.</param>
/// <param name="Page">The page, from 1; 1 when absent.</param>
internal sealed record BuildSearch(
    [FromQuery(Name = "q")] string? Text,
    [FromQuery(Name = "visibility")] string? Visibility,
    [FromQuery(Name = "page")] int? Page);
