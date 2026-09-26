using LoDb.Domain.Languages;
using Microsoft.AspNetCore.WebUtilities;

namespace LoDb.Api.Modules.Accounts.Links;

/// <summary>
/// The pages of the web front the API sends a browser or an e-mail to, under the locale
/// prefix of its routes, such as <c>/fr/verify-email</c>.
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

    /// <summary>Parameter of the e-mail links: the token, in base64url.</summary>
    public const string TokenParameter = "token";

    /// <summary>The path of <paramref name="page"/> in <paramref name="locale"/>.</summary>
    public static string Path(UiLocale locale, string page) => $"/{UiLocales.Code(locale)}/{page}";

    /// <summary>The login page, telling why a sign-in failed.</summary>
    public static string LoginFailed(UiLocale locale, string error) =>
        QueryHelpers.AddQueryString(Path(locale, Login), ErrorParameter, error);
}
