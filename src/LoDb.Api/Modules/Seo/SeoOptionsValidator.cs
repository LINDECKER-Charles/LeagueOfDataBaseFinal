using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Seo;

/// <summary>
/// Refuses at startup a canonical origin that is not a bare http(s) origin, since a path or a
/// query would be copied into every URL of the sitemaps, and an IndexNow key the protocol does
/// not accept.
/// </summary>
internal sealed partial class SeoOptionsValidator : IValidateOptions<SeoOptions>
{
    private const string Root = "/";

    public ValidateOptionsResult Validate(string? name, SeoOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        var origin = options.CanonicalOrigin;
        if (!string.IsNullOrEmpty(origin) && !IsBareOrigin(origin))
        {
            failures.Add($"CanonicalOrigin must read as https://host[:port], not '{origin}'.");
        }

        var key = options.IndexNowKey;
        if (!string.IsNullOrEmpty(key) && !IndexNowKey().IsMatch(key))
        {
            failures.Add("IndexNowKey must be 8 to 128 letters, digits or dashes.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>The origin with its trailing slash dropped, once it validated.</summary>
    public static string Normalize(string origin) =>
        new Uri(origin, UriKind.Absolute).GetLeftPart(UriPartial.Authority);

    private static bool IsBareOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && uri.AbsolutePath == Root
        && uri.UserInfo.Length == 0
        && uri.Query.Length == 0
        && uri.Fragment.Length == 0;

    // The protocol's alphabet, ASCII only: the key also names the file at the site's root.
    [GeneratedRegex("^[a-zA-Z0-9-]{8,128}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex IndexNowKey();
}
