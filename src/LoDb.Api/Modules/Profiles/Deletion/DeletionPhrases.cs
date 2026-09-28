using LoDb.Domain.Languages;

namespace LoDb.Api.Modules.Profiles.Deletion;

/// <summary>
/// The phrase a Google account types to confirm its erasure, <c>profile.danger.confirm_phrase</c>
/// of the legacy catalogs: only French translates it, every other locale falls back to English.
/// </summary>
internal static class DeletionPhrases
{
    public const string English = "DELETE MY ACCOUNT";
    public const string French = "SUPPRIMER MON COMPTE";

    public static string For(UiLocale locale) => locale == UiLocale.Fr ? French : English;

    /// <summary>
    /// Whether <paramref name="typed"/> is the phrase of <paramref name="locale"/>, trimmed and
    /// whatever its case.
    /// </summary>
    public static bool Matches(string? typed, UiLocale locale)
    {
        var trimmed = typed?.Trim();
        return !string.IsNullOrEmpty(trimmed)
            && string.Equals(trimmed, For(locale), StringComparison.OrdinalIgnoreCase);
    }
}
