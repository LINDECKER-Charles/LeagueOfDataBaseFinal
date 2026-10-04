using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace LoDb.Api.Modules.Legacy.Paths;

/// <summary>
/// The old pages that are not the catalog's, and their new path below the locale: the
/// editorial pages keep theirs, the account pages move under <c>account/</c>.
/// </summary>
/// <remarks>
/// Only the pages a visitor could open with a GET are listed: the old form targets (profile
/// identity, password, delete, version…) were never linked, and stay unknown.
/// </remarks>
internal static partial class LegacyPages
{
    private const string Separator = "/";

    private static readonly FrozenDictionary<string, string> Fixed =
        new Dictionary<string, string>
        {
            ["home"] = string.Empty,
            ["trends"] = "trends",
            ["about"] = "about",
            ["about/data"] = "about/data",
            ["faq"] = "faq",
            ["changelog"] = "changelog",
            ["developers"] = "developers",
            ["donate"] = "donate",
            ["legal/notice"] = "legal/notice",
            ["legal/privacy"] = "legal/privacy",
            ["legal/terms"] = "legal/terms",
            ["legal/cookies"] = "legal/cookies",
            ["login"] = "account/login",
            ["register"] = "account/register",
            ["profile"] = "account/profile",
            ["profile/preview"] = "account/profile/preview",
            ["profile/api"] = "account/api",
            ["reset-password"] = "account/forgot-password",
            ["reset-password/check-email"] = "account/forgot-password",
            ["builds"] = "account/builds",
            ["builds/new"] = "account/builds/new",
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The new path of an old page, or <see langword="null"/> when none matches.</summary>
    public static LegacyPath? Find(IReadOnlyList<string> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (Fixed.TryGetValue(string.Join(Separator, segments), out var page))
        {
            return LegacyPath.Page(page);
        }

        var parameterized = segments switch
        {
            ["u", var username] when Username().IsMatch(username) => $"u/{username}",
            ["reset-password", "reset", var token] when ResetToken().IsMatch(token) =>
                $"account/reset-password/{token}",
            ["builds", var id, "edit" or "import"] when BuildId().IsMatch(id) =>
                $"account/builds/{id}/{segments[^1]}",
            _ => null,
        };
        return parameterized is null ? null : LegacyPath.Page(parameterized);
    }

    // The old route requirement of /u/{username}, which is also the registration rule.
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_.\-]{2,23}\z")]
    private static partial Regex Username();

    // Wider than the old tokens, still URL-safe: the new page tells an expired one apart.
    [GeneratedRegex(@"^[A-Za-z0-9_\-]{1,128}\z")]
    private static partial Regex ResetToken();

    [GeneratedRegex(@"^[0-9]{1,18}\z")]
    private static partial Regex BuildId();
}
