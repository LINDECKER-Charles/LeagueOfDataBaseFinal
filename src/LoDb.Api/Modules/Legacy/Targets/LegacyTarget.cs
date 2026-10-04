using LoDb.Domain.Languages;
using LoDb.Domain.Versions;

namespace LoDb.Api.Modules.Legacy.Targets;

/// <summary>
/// Writes the new URL of a redirect: <c>/{locale}/[{version}/]{path}[?lang={variant}]</c>.
/// </summary>
/// <remarks>
/// A path, not an absolute URL: the browser keeps the host it asked, which nginx already
/// made canonical. Every part is URL-safe by construction (locale codes, dotted versions,
/// canonical paths, the page table), so nothing is escaped here.
/// </remarks>
internal static class LegacyTarget
{
    private const string LanguageQuery = "?lang=";

    /// <param name="locale">The locale, and the variant it keeps.</param>
    /// <param name="version">The version the path names; <see langword="null"/> for none.</param>
    /// <param name="path">The path below the locale and version, without leading slash.</param>
    public static string Of(LegacyLocale locale, PatchVersion? version, string path)
    {
        ArgumentNullException.ThrowIfNull(locale);
        ArgumentNullException.ThrowIfNull(path);
        var versionPart = version is null ? string.Empty : $"{version.Value}/";
        var query = locale.Variant is null ? string.Empty : LanguageQuery + locale.Variant.Code;
        return $"/{UiLocales.Code(locale.Locale)}/{versionPart}{path}{query}";
    }
}
