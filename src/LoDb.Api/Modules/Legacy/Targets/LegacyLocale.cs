using LoDb.Domain.Languages;

namespace LoDb.Api.Modules.Legacy.Targets;

/// <summary>
/// The locale a redirect lands on, and the regional Data Dragon language it keeps as
/// <c>?lang=</c> when the old URL asked for one its locale does not open on.
/// </summary>
internal sealed record LegacyLocale
{
    /// <summary>English, what crawlers saw of the old site (ADR 0005).</summary>
    public static LegacyLocale Fallback { get; } = new() { Locale = UiLocales.Fallback };

    public required UiLocale Locale { get; init; }

    /// <summary>A variant such as en_GB; <see langword="null"/> for the locale's own.</summary>
    public DdragonLanguage? Variant { get; init; }
}
