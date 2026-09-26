using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Accounts.Links;
using LoDb.Domain.Languages;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Accounts.Google.Web;

/// <summary>
/// <c>GET /api/account/google/start</c>: sends the browser to Google, which sends it back to
/// the callback. A top-level navigation, not a call of the API client.
/// </summary>
internal sealed class GoogleStartEndpoint(IAuthenticationSchemeProvider schemes)
{
    public static void Map(IEndpointRouteBuilder account) =>
        account.MapGet(
                "/google/start",
                static (
                    [AsParameters] GoogleStartRequest request,
                    [FromServices] GoogleStartEndpoint endpoint,
                    HttpContext context) => endpoint.StartAsync(request, context))
            .WithName("startGoogleSignIn")
            .WithSummary("Sends the browser to Google, to come back signed in.")
            .Produces(StatusCodes.Status302Found);

    public async Task<Results<ChallengeHttpResult, RedirectHttpResult>> StartAsync(
        GoogleStartRequest request,
        HttpContext context)
    {
        var locale = UiLocales.TryParse(request.Locale, out var parsed)
            ? parsed
            : UiLocales.Fallback;
        if (await schemes.GetSchemeAsync(GoogleDefaults.AuthenticationScheme) is null)
        {
            return TypedResults.Redirect(
                FrontPages.LoginFailed(locale, GoogleFailures.Unavailable));
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = SafeReturnUrl.Resolve(request.ReturnUrl, context.Request)
                ?? FrontPages.Path(locale, FrontPages.Profile),
            IsPersistent = request.RememberMe == true,
        };
        properties.Items[GoogleCallback.LocaleItem] = UiLocales.Code(locale);
        return TypedResults.Challenge(properties, [GoogleDefaults.AuthenticationScheme]);
    }
}
