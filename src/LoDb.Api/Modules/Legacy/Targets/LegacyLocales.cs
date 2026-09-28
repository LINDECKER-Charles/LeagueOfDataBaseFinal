using LoDb.Domain.Languages;

namespace LoDb.Api.Modules.Legacy.Targets;

/// <summary>
/// Turns the old <c>?lang=</c>, a Data Dragon language, into the locale of the new URL:
/// <c>fr_FR</c> gives <c>fr</c>, no or a malformed language gives <c>en</c>.
/// </summary>
internal static class LegacyLocales
{
    /// <param name="language">The old <c>?lang=</c>, as received.</param>
    /// <param name="listed">
    /// The languages Data Dragon lists: a regional variant is kept only if it is one of them,
    /// the new site reading nothing else from <c>?lang=</c>.
    /// </param>
    public static LegacyLocale Choose(string? language, IReadOnlyCollection<DdragonLanguage> listed)
    {
        ArgumentNullException.ThrowIfNull(listed);
        if (!DdragonLanguage.TryParse(language, out var requested))
        {
            return LegacyLocale.Fallback;
        }

        var locale = UiLocales.FromLanguage(requested.Code);
        var isVariant = requested != UiLocales.DefaultLanguage(locale)
            && listed.Contains(requested);
        return new LegacyLocale { Locale = locale, Variant = isVariant ? requested : null };
    }
}
