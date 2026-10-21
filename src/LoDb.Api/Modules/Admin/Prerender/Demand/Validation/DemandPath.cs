using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Validation;

/// <summary>
/// A page of an older patch as the queue keeps it, <c>/fr/15.13.1/items/1036-long-sword</c>,
/// cut into what the pass's key and its catalogue need.
/// </summary>
internal sealed record DemandPath
{
    private const char Separator = '/';

    // "", the locale, the version, the type, then the entity of a detail page.
    private const int ListSegments = 4;
    private const int EntitySegments = 5;
    private const int PageStart = 3;

    public required string Path { get; init; }

    public required PatchVersion Version { get; init; }

    public required UiLocale Locale { get; init; }

    public required ResourceType Type { get; init; }

    /// <summary>The page below <c>/{locale}/{version}/</c>, as the catalogue lists it.</summary>
    public required string PagePath { get; init; }

    /// <summary>The locale as the URLs and the unit rows spell it: <c>fr</c>.</summary>
    public string LocaleCode => UiLocales.Code(Locale);

    /// <summary>
    /// The parts of <paramref name="path"/>, written as <c>refusedPagePathOf</c> writes it;
    /// null for any other shape. Whether the page exists is the catalogue's to say.
    /// </summary>
    public static DemandPath? Parse(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var segments = path.Split(Separator);
        if (segments.Length is not (ListSegments or EntitySegments) || segments[0].Length != 0)
        {
            return null;
        }

        return UiLocales.TryParse(segments[1], out var locale)
            && PatchVersion.TryParse(segments[2], out var version)
            && PageTypes.TryParse(segments[PageStart], out var type)
            && Array.TrueForAll(segments[PageStart..], static segment => segment.Length > 0)
                ? new DemandPath
                {
                    Path = path,
                    Version = version,
                    Locale = locale,
                    Type = type,
                    PagePath = string.Join(Separator, segments[PageStart..]),
                }
                : null;
    }
}
