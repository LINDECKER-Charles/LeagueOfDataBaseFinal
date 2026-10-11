using System.Globalization;
using LoDb.Domain.Languages;
using Microsoft.AspNetCore.WebUtilities;

namespace LoDb.Api.Modules.Accounts.Links;

/// <summary>
/// The account pages of the web front the API sends a browser or an e-mail to, under the
/// locale prefix and the <c>account</c> section of its routes (L3.1), such as
/// <c>/fr/account/verify-email</c>.
/// </summary>
internal static class FrontPages
{
    public const string Login = "login";
    public const string Profile = "profile";
    public const string VerifyEmail = "verify-email";
    public const string ResetPassword = "reset-password";

    /// <summary>Parameter of the login page: why a Google sign-in failed.</summary>
    public const string ErrorParameter = "error";

    /// <summary>Parameter of the e-mail links: the id of the account.</summary>
    public const string UserParameter = "user";

    /// <summary>
    /// Parameter of the e-mail links' fragment: the token, in base64url. A fragment never
    /// leaves the browser: no server, log or referrer sees the token.
    /// </summary>
    public const string TokenParameter = "token";

    private const string Section = "account";

    /// <summary>The path of <paramref name="page"/> in <paramref name="locale"/>.</summary>
    public static string Path(UiLocale locale, string page) =>
        $"/{UiLocales.Code(locale)}/{Section}/{page}";

    /// <summary>The login page, telling why a sign-in failed.</summary>
    public static string LoginFailed(UiLocale locale, string error) =>
        QueryHelpers.AddQueryString(Path(locale, Login), ErrorParameter, error);

    /// <summary>
    /// The page verifying an e-mail: <c>verify-email?user={id}#token={token}</c>, the token
    /// already in base64url.
    /// </summary>
    public static string VerifyEmailLink(UiLocale locale, int userId, string token) =>
        Link(Path(locale, VerifyEmail), userId, token);

    /// <summary>
    /// The page setting a new password: <c>reset-password?user={id}#token={token}</c>, the
    /// token already in base64url.
    /// </summary>
    public static string ResetPasswordLink(UiLocale locale, int userId, string token) =>
        Link(Path(locale, ResetPassword), userId, token);

    // The front reads the token from the fragment, then takes it out of the address bar.
    private static string Link(string page, int userId, string token)
    {
        var withUser = QueryHelpers.AddQueryString(
            page,
            UserParameter,
            userId.ToString(CultureInfo.InvariantCulture));
        return $"{withUser}#{TokenParameter}={Uri.EscapeDataString(token)}";
    }
}
