using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Seo;

/// <summary>
/// Refuses at startup a canonical origin that is not a bare http(s) origin: a path or a
/// query would be copied into every URL of the sitemaps.
/// </summary>
internal sealed class SeoOptionsValidator : IValidateOptions<SeoOptions>
{
    private const string Root = "/";

    public ValidateOptionsResult Validate(string? name, SeoOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var origin = options.CanonicalOrigin;
        if (string.IsNullOrEmpty(origin) || IsBareOrigin(origin))
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(
            $"CanonicalOrigin must read as https://host[:port], not '{origin}'.");
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
