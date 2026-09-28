using System.Text.Json;
using LoDb.Api.Modules.Accounts.Authentication;
using LoDb.Api.Modules.Accounts.Google.Web;
using LoDb.Api.Modules.Accounts.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Accounts.Google;

/// <summary>
/// The Google handler of the web flow: authorization code with PKCE, back on
/// <c>/api/account/google/callback</c>, where the account is signed in.
/// </summary>
/// <remarks>
/// Registered only with a web client id: the handler checks its options on every request,
/// and would fail them all without one. The start endpoint then answers
/// <c>google-unavailable</c>.
/// </remarks>
internal static class GoogleSetup
{
    public static AuthenticationBuilder AddAccountsGoogle(
        this AuthenticationBuilder authentication,
        IConfiguration configuration)
    {
        if (string.IsNullOrEmpty(configuration[GoogleAccountOptions.ClientIdKey]))
        {
            return authentication;
        }

        authentication.AddGoogle(static _ => { });
        authentication.Services.AddOptions<GoogleOptions>(GoogleDefaults.AuthenticationScheme)
            .Configure<IOptions<AccountsOptions>, AccountCookies>(Configure);
        return authentication;
    }

    private static void Configure(
        GoogleOptions options,
        IOptions<AccountsOptions> accounts,
        AccountCookies cookies)
    {
        var google = accounts.Value.Google;
        options.ClientId = google.ClientId ?? string.Empty;
        options.ClientSecret = google.ClientSecret ?? string.Empty;
        options.CallbackPath = AccountRoutes.GoogleCallback;
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.UsePkce = true;

        // Google comes back by a top-level GET, which a Lax cookie follows; None would need
        // Secure, which the local stack over plain HTTP cannot keep.
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.CorrelationCookie.SecurePolicy = cookies.Policy;
        options.ClaimActions.MapCustomJson(GoogleProfile.EmailVerifiedClaim, EmailVerified);
        options.Events.OnTicketReceived = static context => context.HttpContext.RequestServices
            .GetRequiredService<GoogleCallback>()
            .CompleteAsync(context);
        options.Events.OnRemoteFailure = GoogleCallback.FailAsync;
    }

    // A boolean at the userinfo endpoint, a string in some of Google's other answers.
    private static string? EmailVerified(JsonElement user) =>
        user.TryGetProperty(GoogleProfile.EmailVerifiedClaim, out var verified)
        && (verified.ValueKind == JsonValueKind.True
            || (verified.ValueKind == JsonValueKind.String
                && verified.GetString() == GoogleProfile.Verified))
            ? GoogleProfile.Verified
            : null;
}
