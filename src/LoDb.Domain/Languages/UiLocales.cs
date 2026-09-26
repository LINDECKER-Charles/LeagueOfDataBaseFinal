using System.Collections.Frozen;
using LoDb.Domain.Text;

namespace LoDb.Domain.Languages;

/// <summary>
/// Codes of the interface locales and their mapping to and from Data Dragon languages.
/// </summary>
/// <remarks>
/// A locale opens on one default Data Dragon language; a regional variant (<c>en_GB</c>,
/// <c>es_MX</c>…) stays a preference layered on top. The reverse mapping reduces a language
/// to its base code, with the two Chinese scripts kept apart, and falls back to English.
/// </remarks>
public static class UiLocales
{
    /// <summary>Locale used when nothing better is known.</summary>
    public const UiLocale Fallback = UiLocale.En;

    // Every Chinese language but Taiwan's is written in simplified characters (zh_CN, zh_MY).
    private const string ChinesePrefix = "zh_";
    private const string TraditionalChinese = "zh_TW";
    private const int BaseCodeLength = 2;

    // es_ES, not es_MX, for Spanish: it ships with every version of the audit, es_MX does not.
    private static readonly FrozenDictionary<UiLocale, (string Code, DdragonLanguage Language)>
        Table = new Dictionary<UiLocale, (string, DdragonLanguage)>
        {
            [UiLocale.Ar] = ("ar", DdragonLanguage.Parse("ar_AE")),
            [UiLocale.Cs] = ("cs", DdragonLanguage.Parse("cs_CZ")),
            [UiLocale.De] = ("de", DdragonLanguage.Parse("de_DE")),
            [UiLocale.El] = ("el", DdragonLanguage.Parse("el_GR")),
            [UiLocale.En] = ("en", DdragonLanguage.EnUs),
            [UiLocale.Es] = ("es", DdragonLanguage.Parse("es_ES")),
            [UiLocale.Fr] = ("fr", DdragonLanguage.Parse("fr_FR")),
            [UiLocale.Hu] = ("hu", DdragonLanguage.Parse("hu_HU")),
            [UiLocale.Id] = ("id", DdragonLanguage.Parse("id_ID")),
            [UiLocale.It] = ("it", DdragonLanguage.Parse("it_IT")),
            [UiLocale.Ja] = ("ja", DdragonLanguage.Parse("ja_JP")),
            [UiLocale.Ko] = ("ko", DdragonLanguage.Parse("ko_KR")),
            [UiLocale.Pl] = ("pl", DdragonLanguage.Parse("pl_PL")),
            [UiLocale.Pt] = ("pt", DdragonLanguage.Parse("pt_BR")),
            [UiLocale.Ro] = ("ro", DdragonLanguage.Parse("ro_RO")),
            [UiLocale.Ru] = ("ru", DdragonLanguage.Parse("ru_RU")),
            [UiLocale.Th] = ("th", DdragonLanguage.Parse("th_TH")),
            [UiLocale.Tr] = ("tr", DdragonLanguage.Parse("tr_TR")),
            [UiLocale.Vi] = ("vi", DdragonLanguage.Parse("vi_VN")),
            [UiLocale.ZhHans] = ("zh-hans", DdragonLanguage.Parse("zh_CN")),
            [UiLocale.ZhHant] = ("zh-hant", DdragonLanguage.Parse("zh_TW")),
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, UiLocale> ByCode = Table.ToFrozenDictionary(
        entry => entry.Value.Code,
        entry => entry.Key,
        StringComparer.Ordinal);

    /// <summary>Every locale, in declaration order.</summary>
    public static IReadOnlyList<UiLocale> All { get; } = [.. Enum.GetValues<UiLocale>()];

    /// <summary>URL segment of the locale ("fr", "zh-hans").</summary>
    public static string Code(UiLocale locale) => Table[locale].Code;

    /// <summary>Reads a locale code exactly as <see cref="Code"/> writes it.</summary>
    public static bool TryParse(string? code, out UiLocale locale) =>
        ByCode.TryGetValue(code ?? string.Empty, out locale);

    /// <summary>Data Dragon language a locale opens on.</summary>
    public static DdragonLanguage DefaultLanguage(UiLocale locale) => Table[locale].Language;

    /// <summary>Locale whose interface suits a Data Dragon language code.</summary>
    public static UiLocale FromLanguage(string? language)
    {
        if (string.IsNullOrEmpty(language))
        {
            return Fallback;
        }

        if (language.StartsWith(TraditionalChinese, StringComparison.Ordinal))
        {
            return UiLocale.ZhHant;
        }

        if (language.StartsWith(ChinesePrefix, StringComparison.Ordinal))
        {
            return UiLocale.ZhHans;
        }

        return language.Length >= BaseCodeLength
            && ByCode.TryGetValue(AsciiCase.Lower(language[..BaseCodeLength]), out var locale)
                ? locale
                : Fallback;
    }
}
