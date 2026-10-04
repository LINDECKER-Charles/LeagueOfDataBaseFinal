using System.Collections.Frozen;
using System.Text.Json;
using LoDb.Domain.Languages;

namespace LoDb.Infrastructure.Outbox.Rendering;

/// <summary>
/// The <c>email.*</c> texts of the legacy translations, one catalog per locale that has
/// them; every other locale, and every key a catalog lacks, falls back to English.
/// </summary>
/// <remarks>
/// The legacy stack only translated its e-mails in English and French: the other locales
/// received English, which they still do.
/// </remarks>
internal static class EmailTexts
{
    /// <summary>Locale of the catalog that holds every key.</summary>
    public const UiLocale Fallback = UiLocale.En;

    private static readonly FrozenDictionary<UiLocale, FrozenDictionary<string, string>> Catalogs =
        EmailResources.TextCatalogs().ToFrozenDictionary(
            static catalog => UiLocales.TryParse(catalog.Code, out var locale)
                ? locale
                : throw new InvalidOperationException(
                    $"The e-mail catalog {catalog.Code} names no locale."),
            static catalog => Parse(catalog.Json));

    /// <summary>Locales that have a catalog of their own.</summary>
    public static IReadOnlyCollection<UiLocale> Translated => Catalogs.Keys;

    /// <summary>The texts an e-mail to <paramref name="locale"/> is written with.</summary>
    public static LocalizedTexts For(UiLocale locale) =>
        new(
            Catalogs.ContainsKey(locale) ? locale : Fallback,
            Catalogs.GetValueOrDefault(locale),
            Catalogs[Fallback]);

    private static FrozenDictionary<string, string> Parse(string json) =>
        (JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            ?? throw new InvalidOperationException("An e-mail catalog is empty."))
        .ToFrozenDictionary(StringComparer.Ordinal);
}
