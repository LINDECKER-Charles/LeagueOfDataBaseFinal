using System.Diagnostics.CodeAnalysis;

namespace LoDb.Desktop.Bridge.Validation;

/// <summary>
/// A URL the page may open in the system browser: absolute http(s) of a remote host. Any
/// other scheme could start a local program through its protocol handler (<c>file:</c>,
/// <c>ms-msdt:</c>…), which is what the bridge must never be used for.
/// </summary>
internal static class ExternalUrl
{
    /// <summary>Longest URL accepted, the de facto limit of browsers and servers.</summary>
    public const int MaxLength = 2048;

    public static bool TryParse(string? value, [NotNullWhen(true)] out Uri? url)
    {
        url = null;
        if (string.IsNullOrEmpty(value)
            || value.Length > MaxLength
            || value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character))
            || !Uri.TryCreate(value, UriKind.Absolute, out var candidate)
            || !IsRemoteWebAddress(candidate))
        {
            return false;
        }

        url = candidate;
        return true;
    }

    // Credentials in a URL are a phishing device ("https://site@evil"); loopback addresses
    // are the app's own or another local service, never a page to send the user to.
    private static bool IsRemoteWebAddress(Uri url) =>
        (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp)
        && url.Host.Length > 0
        && url.UserInfo.Length == 0
        && !url.IsLoopback;
}
