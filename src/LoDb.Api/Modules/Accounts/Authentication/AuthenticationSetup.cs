using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Api.Modules.Accounts.Authentication;

/// <summary>
/// The schemes of the accounts: the session cookie of the web, Identity's bearer and refresh
/// tokens for the apps, and the antiforgery pair behind the XSRF token.
/// </summary>
/// <remarks>
/// Neither the tokens nor the cookie are stored on the server: they are Data Protection
/// payloads, whose keys every instance shares. A changed security stamp or a ban reaches
/// them at the next revalidation of the cookie, and when the access token runs out.
/// </remarks>
internal static class AuthenticationSetup
{
    /// <summary>
    /// How long a session cookie is trusted before it is checked against its account again.
    /// </summary>
    public static readonly TimeSpan RevalidationInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Lifetime of an access token: never checked against its account, it is short so that
    /// a ban reaches the apps as fast as the web.
    /// </summary>
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(5);

    /// <summary>Lifetime of a refresh token, as long as "remember me".</summary>
    public static readonly TimeSpan RefreshTokenLifetime = SessionCookie.RememberMeLifetime;

    /// <summary>Lifetime of the e-mail verification and password reset tokens.</summary>
    public static readonly TimeSpan EmailTokenLifetime = TimeSpan.FromHours(1);

    public static AuthenticationBuilder AddAccountsAuthentication(this IServiceCollection services)
    {
        services.TryAddSingleton<AccountCookies>();
        services.TryAddSingleton<XsrfTokens>();
        services.AddAntiforgery();
        services.AddOptions<AntiforgeryOptions>().Configure<AccountCookies>(ConfigureAntiforgery);
        services.AddOptions<SecurityStampValidatorOptions>().Configure<TimeProvider>(Revalidate);
        services.AddOptions<DataProtectionTokenProviderOptions>()
            .Configure(static options => options.TokenLifespan = EmailTokenLifetime);
        services.AddOptions<BearerTokenOptions>(IdentityConstants.BearerScheme)
            .Configure<TimeProvider>(ConfigureBearer);

        var authentication = services.AddAuthentication(AccountSchemes.Selector)
            .AddPolicyScheme(
                AccountSchemes.Selector,
                displayName: null,
                static options => options.ForwardDefaultSelector = AccountSchemes.Select)
            .AddBearerToken(IdentityConstants.BearerScheme);
        authentication.AddApplicationCookie()
            .Configure<AccountCookies, TimeProvider>(SessionCookie.Configure);
        authentication.AddExternalCookie().Configure<AccountCookies>(ConfigureExternal);

        // Identity signs this cookie out when a revalidation fails: it must exist, even
        // though no sign-in remembers a second factor.
        authentication.AddTwoFactorRememberMeCookie();
        return authentication;
    }

    private static void ConfigureAntiforgery(AntiforgeryOptions options, AccountCookies cookies)
    {
        options.HeaderName = XsrfTokens.HeaderName;
        options.Cookie.Name = cookies.Antiforgery;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = cookies.Policy;
        options.Cookie.Path = AccountCookies.RootPath;

        // The API only answers JSON, which no frame displays.
        options.SuppressXFrameOptionsHeader = true;
    }

    private static void Revalidate(SecurityStampValidatorOptions options, TimeProvider clock)
    {
        options.ValidationInterval = RevalidationInterval;
        options.TimeProvider = clock;
        options.OnRefreshingPrincipal = AuthenticationMethods.KeepOnRefreshAsync;
    }

    private static void ConfigureBearer(BearerTokenOptions options, TimeProvider clock)
    {
        options.BearerTokenExpiration = AccessTokenLifetime;
        options.RefreshTokenExpiration = RefreshTokenLifetime;
        options.TimeProvider = clock;
    }

    // Written by no sign-in of the API, but Identity signs it out with the session.
    private static void ConfigureExternal(
        CookieAuthenticationOptions options,
        AccountCookies cookies)
    {
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = cookies.Policy;
    }
}
