using LoDb.Api.Modules.Accounts.Authentication;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Accounts.Session;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Accounts.SignIn;

/// <summary>
/// <c>POST /api/account/login</c>: opens the session cookie of the web, and renews its XSRF
/// token, which binds to the account.
/// </summary>
internal sealed class LoginEndpoint(PasswordSignIn passwordSignIn, SessionReader sessions)
{
    public static void Map(IEndpointRouteBuilder account) =>
        account.MapPost(
                "/login",
                static (
                    [FromBody] LoginRequest request,
                    [FromServices] LoginEndpoint endpoint,
                    HttpContext context) => endpoint.LoginAsync(request, context))
            .WithName("signIn")
            .WithSummary("Opens a session with an e-mail or a username and a password.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    public async Task<Results<Ok<AccountSession>, AccountProblem>> LoginAsync(
        LoginRequest request,
        HttpContext context)
    {
        var problem = await passwordSignIn.SignInAsync(
            request.ToCredentials(),
            SignInChannel.Session,
            context.RequestAborted);
        if (problem is not null)
        {
            return problem;
        }

        // The sign-in manager has set the new principal on the request.
        return TypedResults.Ok(await sessions.ReadAsync(context.User));
    }
}
