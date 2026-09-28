using LoDb.Api.Modules.Accounts.Authentication;
using LoDb.Api.Modules.Accounts.Http;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Accounts.SignIn;

/// <summary>
/// <c>POST /api/account/token</c>: the sign-in of the apps, answered with Identity's token
/// response: a bearer token of 5 minutes and a refresh token of 30 days.
/// </summary>
internal sealed class TokenEndpoint(PasswordSignIn passwordSignIn)
{
    public static void Map(IEndpointRouteBuilder account) =>
        account.MapPost(
                "/token",
                static (
                    [FromBody] TokenRequest request,
                    [FromServices] TokenEndpoint endpoint,
                    CancellationToken aborted) => endpoint.CreateAsync(request, aborted))
            .WithName("createAccountToken")
            .WithSummary("Signs an app in with an e-mail or a username and a password.")
            .Produces<AccessTokenResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    public async Task<Results<EmptyHttpResult, AccountProblem>> CreateAsync(
        TokenRequest request,
        CancellationToken cancellationToken)
    {
        var problem = await passwordSignIn.SignInAsync(
            request.ToCredentials(),
            SignInChannel.Token,
            cancellationToken);

        // On success the bearer handler has written the token response.
        return problem is null ? TypedResults.Empty : problem;
    }
}
