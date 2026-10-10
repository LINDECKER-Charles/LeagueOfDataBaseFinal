using LoDb.Domain.Languages;

namespace LoDb.Ingestion.Catalog.Reading;

/// <summary>
/// Reads <c>LoDb:Catalog:PinnedLanguages</c>: the languages whose latest catalog is pinned,
/// most read first.
/// </summary>
internal static class PinnedLanguageList
{
    private const char Separator = ',';

    /// <summary>
    /// The language of every interface locale, en_US first: every other catalog is built
    /// over it, and it answers the crawlers, the trends and the analytics.
    /// </summary>
    public static IReadOnlyList<DdragonLanguage> Defaults { get; } =
    [
        DdragonLanguage.EnUs,
        .. UiLocales.All
            .Select(UiLocales.DefaultLanguage)
            .Where(static language => language != DdragonLanguage.EnUs),
    ];

    /// <returns>
    /// The languages in the order given, each once; the defaults when the setting is empty;
    /// <see langword="null"/> when a code is malformed.
    /// </returns>
    public static IReadOnlyList<DdragonLanguage>? Parse(string? setting)
    {
        if (string.IsNullOrWhiteSpace(setting))
        {
            return Defaults;
        }

        List<DdragonLanguage> languages = [];
        var codes = setting.Split(
            Separator,
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var code in codes)
        {
            if (!DdragonLanguage.TryParse(code, out var language))
            {
                return null;
            }

            if (!languages.Contains(language))
            {
                languages.Add(language);
            }
        }

        return languages;
    }
}
