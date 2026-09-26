using LoDb.Api.Hosting;

namespace LoDb.Api.Modules.Accounts.Http;

/// <summary>Paths and OpenAPI tag of the accounts API.</summary>
internal static class AccountRoutes
{
    /// <summary>Prefix of every account endpoint.</summary>
    public const string Prefix = ApiPaths.App + "/account";

    /// <summary>The session of the caller, where a new account's <c>Location</c> points.</summary>
    public const string Me = Prefix + "/me";

    /// <summary>
    /// Where Google sends the browser back: the redirect URI of the web client in the Google
    /// console, on every origin the site answers on.
    /// </summary>
    public const string GoogleCallback = Prefix + "/google/callback";

    /// <summary>The generated client gets one service per tag.</summary>
    public const string Tag = "Account";
}
