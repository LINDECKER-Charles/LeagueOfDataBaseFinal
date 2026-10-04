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

    /// <summary>Parameter of the verification link: the token, in base64url.</summary>
    public const string TokenParameter = "token";

    private const string Section = "account";

    /// <summary>The path of <paramref name="page"/> in <paramref name="locale"/>.</summary>
    public static string Path(UiLocale locale, string page) =>
        $"/{UiLocales.Code(locale)}/{Section}/{page}";

    /// <summary>The login page, telling why a sign-in failed.</summary>
    public static string LoginFailed(UiLocale locale, string error) =>
        QueryHelpers.AddQueryString(Path(locale, Login), ErrorParameter, error);

    /// <summary>
    /// The page verifying an e-mail: <c>verify-email?user={id}&amp;token={token}</c>, the
    /// token already in base64url.
    /// </summary>
    public static string VerifyEmailLink(UiLocale locale, int userId, string token) =>
        QueryHelpers.AddQueryString(
            Path(locale, VerifyEmail),
            new Dictionary<string, string?>
            {
                [UserParameter] = userId.ToString(CultureInfo.InvariantCulture),
                [TokenParameter] = token,
            });

    /// <summary>
    /// The page setting a new password: <c>reset-password/{token}?user={id}</c>, the token
    /// (already in base64url) in the path, as the front's route reads it.
    /// </summary>
    public static string ResetPasswordLink(UiLocale locale, int userId, string token) =>
        QueryHelpers.AddQueryString(
            $"{Path(locale, ResetPassword)}/{Uri.EscapeDataString(token)}",
            UserParameter,
            userId.ToString(CultureInfo.InvariantCulture));
}
