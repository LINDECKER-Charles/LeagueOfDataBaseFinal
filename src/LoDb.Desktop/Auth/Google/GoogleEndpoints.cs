using LoDb.Desktop.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Desktop.Auth.Google;

/// <summary>
/// <c>GET /desktop/auth/google</c> opens Google's page in the system browser (plan §5.2);
/// <c>GET /desktop/auth/google/callback</c> is its loopback redirect URI.
/// </summary>
internal sealed class GoogleEndpoints(GoogleSignIn google, IHostApplicationLifetime lifetime)
{
    private const string BeginPath = "/google";
    private const string CallbackPath = "/google/callback";
    private const string SessionPath = DesktopRoutes.Auth + "/session";

    public static void Map(IEndpointRouteBuilder auth)
    {
        auth.MapGet(
            BeginPath,
            static (
                [FromQuery] bool? rememberMe,
                HttpRequest request,
                [FromServices] GoogleEndpoints endpoints) =>
                endpoints.Begin(request, rememberMe ?? false));
        auth.MapGet(
            CallbackPath,
            static (
                [AsParameters] GoogleCallbackQuery callback,
                HttpResponse response,
                [FromServices] GoogleEndpoints endpoints) =>
                endpoints.CompleteAsync(callback, response));
    }

    /// <summary>
    /// 202 with the session to poll, 503 without a Google client, 500 without a browser.
    /// </summary>
    public IResult Begin(HttpRequest request, bool isRemembered)
    {
        // The guard has checked the Host: it is this loopback origin, port included.
        var redirectUri = new Uri(
            $"{request.Scheme}://{request.Host}{DesktopRoutes.GoogleCallback}");
        return google.Begin(redirectUri, isRemembered) switch
        {
            null => TypedResults.Accepted(SessionPath),
            GoogleFailures.Unavailable => ProblemRelay.Problem(
                StatusCodes.Status503ServiceUnavailable,
                GoogleFailures.Unavailable),
            var failure => ProblemRelay.Problem(StatusCodes.Status500InternalServerError, failure),
        };
    }

    // The exchange is tied to the app's life, not to the browser tab: closing the tab early
    // must not leave the flow half done.
    public async Task<IResult> CompleteAsync(GoogleCallbackQuery callback, HttpResponse response)
    {
        var failure = await google.CompleteAsync(callback, lifetime.ApplicationStopping);
        return GoogleCallbackPage.For(failure, response);
    }
}
