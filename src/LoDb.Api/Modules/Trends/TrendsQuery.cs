using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Domain.Languages;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Trends;

/// <summary>
/// The query of the trends: <c>?champion=&amp;mode=&amp;language=&amp;page=</c> to filter
/// and page the builds, <c>?version=&amp;lang=</c> for the names, all optional.
/// </summary>
/// <param name="Champion">A champion id, such as MonkeyKing.</param>
/// <param name="Mode">A mode code, such as aram; an unknown one filters nothing.</param>
/// <param name="Language">
/// The Data Dragon language the builds are written in; an unknown one filters nothing.
/// </param>
/// <param name="Page">From 1, 24 builds a page; below 1 reads as 1.</param>
/// <param name="Version">The version of the names; the latest when unset.</param>
/// <param name="Lang">The Data Dragon language of the names; en_US when unset.</param>
/// <param name="Context">The call.</param>
internal sealed record TrendsQuery(
    [FromQuery(Name = "champion")] string? Champion,
    [FromQuery(Name = "mode")] string? Mode,
    [FromQuery(Name = "language")] string? Language,
    [FromQuery(Name = "page")] int? Page,
    [FromQuery(Name = "version")] string? Version,
    [FromQuery(Name = "lang")] string? Lang,
    HttpContext Context)
{
    // Past it, the first build of the page would lie beyond what an offset can count.
    private const int MaxPage = int.MaxValue / TrendsRanking.PerPage;

    public int PageNumber => Math.Clamp(Page ?? 1, 1, MaxPage);

    public CatalogScope Browsing => new(Version, Lang ?? DdragonLanguage.EnUs.Code);
}
