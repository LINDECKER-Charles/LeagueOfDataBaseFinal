using LoDb.Api.Modules.Accounts.Authentication;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Accounts.SignIn;

/// <summary>
/// <c>POST /api/account/refresh</c>: trades a refresh token for new tokens, as long as the
/// account kept its security stamp and is not banned.
/// </summary>
/// <remarks>
/// A password reset or a ban changes the stamp, and so revokes every refresh token of the
/// account; the access tokens already handed out run out within minutes.
/// </remarks>
internal sealed class RefreshEndpoint(
    IOptionsMonitor<BearerTokenOptions> bearerOptions,
    SignInManager<User> signIn,
    TimeProvider clock)
{
    public static void Map(IEndpointRouteBuilder account) =>
        account.MapPost(
                "/refresh",
                static (
                    [FromBody] RefreshTokenRequest request,
                    [FromServices] RefreshEndpoint endpoint) => endpoint.RefreshAsync(request))
            .WithName("refreshAccountToken")
            .WithSummary("Trades a refresh token for a new bearer token and refresh token.")
            .Produces<AccessTokenResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

    public async Task<Results<SignInHttpResult, AccountProblem>> RefreshAsync(
        RefreshTokenRequest request)
    {
        var errors = new FieldErrors();
        errors.RequireText(AccountFields.RefreshToken, request.RefreshToken);
        if (!errors.IsEmpty)
        {
            return errors.ToProblem();
        }

        var protector = bearerOptions.Get(IdentityConstants.BearerScheme).RefreshTokenProtector;
        var ticket = protector.Unprotect(request.RefreshToken);
        if (ticket?.Properties.ExpiresUtc is not { } expires || clock.GetUtcNow() >= expires
            || await signIn.ValidateSecurityStampAsync(ticket.Principal) is not { } user)
        {
            return AccountProblem.InvalidRefreshToken();
        }

        var principal = await signIn.CreateUserPrincipalAsync(user);
        AuthenticationMethods.Carry(ticket.Principal, principal);
        return TypedResults.SignIn(
            principal,
            authenticationScheme: IdentityConstants.BearerScheme);
    }
}
