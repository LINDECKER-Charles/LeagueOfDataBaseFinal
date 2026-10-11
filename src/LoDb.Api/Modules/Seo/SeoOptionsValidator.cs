using System.Globalization;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Seo;

/// <summary>
/// Refuses at startup a canonical origin that is not a bare http(s) origin, since a path or a
/// query would be copied into every URL of the sitemaps, and a negative sitemap window.
/// </summary>
internal sealed class SeoOptionsValidator : IValidateOptions<SeoOptions>
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

        if (options.SitemapVersionWindow < 0)
        {
            failures.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"SitemapVersionWindow must be 0 or more, not {options.SitemapVersionWindow}."));
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
}
