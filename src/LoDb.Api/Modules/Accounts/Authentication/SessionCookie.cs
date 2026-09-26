using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace LoDb.Api.Modules.Accounts.Authentication;

/// <summary>
/// The session cookie of the web: HttpOnly, <c>SameSite=Lax</c>, and <c>Secure</c> with the
/// <c>__Host-</c> prefix outside the local stack. Nothing is kept on the server.
/// </summary>
internal static class SessionCookie
{
    /// <summary>Lifetime of a session opened with "remember me", renewed while in use.</summary>
    public static readonly TimeSpan RememberMeLifetime = TimeSpan.FromDays(30);

    /// <summary>
    /// Lifetime of any other session: its cookie goes when the browser closes, and a browser
    /// left open does not keep it for thirty days either. Renewed while in use.
    /// </summary>
    public static readonly TimeSpan BrowserSessionLifetime = TimeSpan.FromDays(1);

    public static void Configure(
        CookieAuthenticationOptions options,
        AccountCookies cookies,
        TimeProvider clock)
    {
        options.Cookie.Name = cookies.Session;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = cookies.Policy;
        options.Cookie.Path = AccountCookies.RootPath;
        options.ExpireTimeSpan = RememberMeLifetime;
        options.SlidingExpiration = true;
        options.TimeProvider = clock;

        // The API answers with a status, never with a redirect to a login page. The events
        // object is Identity's, whose principal validation stays.
        options.Events.OnRedirectToLogin =
            static context => Answer(context, StatusCodes.Status401Unauthorized);
        options.Events.OnRedirectToAccessDenied =
            static context => Answer(context, StatusCodes.Status403Forbidden);
        options.Events.OnSigningIn = static context => LimitBrowserSession(context);
        options.Events.OnSignedIn = static context => RenewXsrfToken(context);
    }

    private static Task Answer(RedirectContext<CookieAuthenticationOptions> context, int status)
    {
        context.Response.StatusCode = status;
        return Task.CompletedTask;
    }

    private static Task LimitBrowserSession(CookieSigningInContext context)
    {
        if (!context.Properties.IsPersistent && context.Properties.IssuedUtc is { } issued)
        {
            context.Properties.ExpiresUtc = issued + BrowserSessionLifetime;
        }

        return Task.CompletedTask;
    }

    // The token of the anonymous page no longer validates once someone is signed in.
    private static Task RenewXsrfToken(CookieSignedInContext context)
    {
        if (context.Principal is { } principal)
        {
            context.HttpContext.User = principal;
        }

        context.HttpContext.RequestServices.GetRequiredService<XsrfTokens>()
            .Issue(context.HttpContext);
        return Task.CompletedTask;
    }
}
