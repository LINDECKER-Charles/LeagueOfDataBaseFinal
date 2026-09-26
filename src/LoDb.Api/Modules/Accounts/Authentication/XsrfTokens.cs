using Microsoft.AspNetCore.Antiforgery;

namespace LoDb.Api.Modules.Accounts.Authentication;

/// <summary>
/// The XSRF token of the web front, in Angular's convention: <c>HttpClient</c> reads it from
/// the <c>XSRF-TOKEN</c> cookie and sends it back in <c>X-XSRF-TOKEN</c> on unsafe requests.
/// </summary>
/// <remarks>
/// The token is the request half of an antiforgery pair whose cookie half stays HttpOnly,
/// and it binds to the signed-in account: a sign-in or a sign-out issues a new one.
/// </remarks>
internal sealed class XsrfTokens(IAntiforgery antiforgery, AccountCookies cookies)
{
    public const string CookieName = "XSRF-TOKEN";
    public const string HeaderName = "X-XSRF-TOKEN";

    /// <summary>Issues the token of the current user of <paramref name="context"/>.</summary>
    public void Issue(HttpContext context)
    {
        if (!cookies.CanWrite(context.Request))
        {
            return;
        }

        var tokens = antiforgery.GetAndStoreTokens(context);
        context.Response.Cookies.Append(CookieName, tokens.RequestToken ?? string.Empty, new()
        {
            // Read by the front's script, which is the point of this cookie.
            HttpOnly = false,
            Secure = cookies.Secure || context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = AccountCookies.RootPath,
            IsEssential = true,
        });
    }

    /// <summary>Whether the unsafe request carries the token of its signed-in user.</summary>
    public async Task<bool> IsValidAsync(HttpContext context)
    {
        if (!cookies.CanWrite(context.Request))
        {
            return false;
        }

        try
        {
            return await antiforgery.IsRequestValidAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            // A form body without the header, which could not be read.
            return false;
        }
    }
}
