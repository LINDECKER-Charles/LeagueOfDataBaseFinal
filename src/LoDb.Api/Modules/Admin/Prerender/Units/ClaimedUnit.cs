using LoDb.Api.Modules.Admin.Prerender.Passes;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;

namespace LoDb.Api.Modules.Admin.Prerender.Units;

/// <summary>
/// The unit the worker takes next: one (version, locale) of the running pass, where it
/// stopped, and what it already wrote.
/// </summary>
internal sealed record ClaimedUnit
{
    public required RunningPass Pass { get; init; }

    public required PatchVersion Version { get; init; }

    public required UiLocale Locale { get; init; }

    /// <summary>
    /// The locale as the URLs and the unit's row spell it: <c>fr</c>, <c>zh-hans</c>.
    /// </summary>
    public string LocaleCode => UiLocales.Code(Locale);

    /// <summary>Index in the unit's page list of the next page to render.</summary>
    public int Cursor { get; init; }

    public int PagesTotal { get; init; }

    public int PagesWritten { get; init; }

    public int PagesSkipped { get; init; }

    public int PagesFailed { get; init; }

    public long BytesWritten { get; init; }
}
