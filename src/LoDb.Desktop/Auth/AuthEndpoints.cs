using LoDb.Desktop.Auth.Api;
using LoDb.Desktop.Auth.Google;
using LoDb.Desktop.Auth.Tokens;
using LoDb.Desktop.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Desktop.Auth;

/// <summary>
/// The token endpoints of the front's <c>host</c> strategy (plan §5.2): the page signs in
/// and out through them, and the tokens stay in the host.
/// </summary>
internal sealed class AuthEndpoints(AccountApiClient api, TokenSession session, GoogleFlows flows)
{
    private const string LoginPath = "/login";
    private const string LogoutPath = "/logout";
    private const string SessionPath = "/session";

    public static void MapDesktopAuth(IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup(DesktopRoutes.Auth);
        auth.MapPost(
            LoginPath,
            static (
                [FromBody] DesktopLoginRequest request,
                [FromServices] AuthEndpoints endpoints,
                CancellationToken aborted) => endpoints.LoginAsync(request, aborted));
        auth.MapPost(
            LogoutPath,
            static ([FromServices] AuthEndpoints endpoints, CancellationToken aborted) =>
                endpoints.LogoutAsync(aborted));
        auth.MapGet(
            SessionPath,
            static ([FromServices] AuthEndpoints endpoints, CancellationToken aborted) =>
                endpoints.ReadSessionAsync(aborted));
        GoogleEndpoints.Map(auth);
    }

    /// <summary>
    /// 204 once signed in. A refusal of the API (401, and also 400, 403 or 429) is relayed
    /// with its ProblemDetails; 502 when the API cannot be reached.
    /// </summary>
    public async Task<IResult> LoginAsync(
        DesktopLoginRequest request,
        CancellationToken cancellationToken)
    {
        var call = await api.SignInAsync(request.ToBody(), cancellationToken);
        if (call.Grant is not { } grant)
        {
            return ProblemRelay.FailureOf(call);
        }

        await session.AdoptAsync(grant, request.RememberMe, cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// 204. The API's tokens are stateless: forgetting them, and the saved refresh token, is
    /// the whole sign-out.
    /// </summary>
    public async Task<IResult> LogoutAsync(CancellationToken cancellationToken)
    {
        await session.SignOutAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    public async Task<IResult> ReadSessionAsync(CancellationToken cancellationToken)
    {
        var state = await session.ReadStateAsync(cancellationToken);
        var google = flows.Status;
        return TypedResults.Ok(new DesktopSession
        {
            SignedIn = state.IsSignedIn,
            Remembered = state.IsRemembered,
            Google = google.Stage,
            GoogleFailure = google.Failure,
        });
    }
}
