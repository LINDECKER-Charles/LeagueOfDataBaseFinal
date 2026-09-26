using LoDb.Api.Hosting;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Modules.Accounts.Authentication;

/// <summary>
/// The schemes of the API: the session cookie of the web, the bearer tokens of the apps, and
/// the default scheme, which picks one of them for each request.
/// </summary>
internal static class AccountSchemes
{
    /// <summary>The default scheme: forwards to the bearer handler or to the cookie one.</summary>
    public const string Selector = "LoDb";

    // Case-sensitive, as the bearer handler reads it.
    private const string BearerPrefix = "Bearer ";

    /// <summary>
    /// The bearer handler for an <c>/api</c> request that carries a bearer token, the session
    /// cookie for any other.
    /// </summary>
    public static string Select(HttpContext context) =>
        IsBearer(context.Request)
            ? IdentityConstants.BearerScheme
            : IdentityConstants.ApplicationScheme;

    /// <summary>
    /// Whether the request authenticates by bearer token. Only under <c>/api</c>: the public
    /// API reads its own keys from the same header.
    /// </summary>
    public static bool IsBearer(HttpRequest request) =>
        request.Path.StartsWithSegments(ApiPaths.App)
        && request.Headers.Authorization.ToString()
            .StartsWith(BearerPrefix, StringComparison.Ordinal);
}
