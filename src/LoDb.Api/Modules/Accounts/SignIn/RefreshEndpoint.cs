using LoDb.Api.Modules.Accounts.Authentication;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Accounts.Security;
using LoDb.Api.Modules.Accounts.Tokens;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Accounts.SignIn;

/// <summary>
/// <c>POST /api/account/refresh</c>: trades a refresh token for new tokens, once, as long as
/// the account kept its security stamp, is not banned and its family is not revoked.
/// </summary>
/// <remarks>
/// <para>
/// A refresh token serves once (<see cref="RefreshFamilies"/>): the answer carries the next
/// one, which the app keeps in its place. A spent token played again revokes its family,
/// and the session ends for whoever holds it, the app included.
/// </para>
/// <para>
/// A password reset, a ban or a sign-out everywhere changes the stamp, and so revokes every
/// refresh token of the account; the access tokens already handed out run out within
/// minutes.
/// </para>
/// </remarks>
internal sealed partial class RefreshEndpoint(
    IOptionsMonitor<BearerTokenOptions> bearerOptions,
    SignInManager<User> signIn,
    RefreshFamilies families,
    SecurityMetrics metrics,
    TimeProvider clock,
    ILogger<RefreshEndpoint> logger)
{
    public static void Map(IEndpointRouteBuilder account) =>
        account.MapPost(
                "/refresh",
                static (
                    [FromBody] RefreshTokenRequest request,
                    [FromServices] RefreshEndpoint endpoint,
                    CancellationToken aborted) => endpoint.RefreshAsync(request, aborted))
            .WithName("refreshAccountToken")
            .WithSummary("Trades a refresh token, once, for a new bearer token and refresh token.")
            .Produces<AccessTokenResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

    public async Task<Results<SignInHttpResult, AccountProblem>> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
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

        var link = await RotateAsync(user, ticket, request.RefreshToken!, cancellationToken);
        if (link is null)
        {
            return AccountProblem.InvalidRefreshToken();
        }

        var principal = await signIn.CreateUserPrincipalAsync(user);
        AuthenticationMethods.Carry(ticket.Principal, principal);
        RefreshFamilyClaims.AddTo(principal, link);
        return TypedResults.SignIn(
            principal,
            authenticationScheme: IdentityConstants.BearerScheme);
    }

    // A token handed out before the families gets the one named after it first.
    private async Task<RefreshLink?> RotateAsync(
        User user,
        AuthenticationTicket ticket,
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var presented = RefreshFamilyClaims.Read(ticket.Principal);
        if (presented is null)
        {
            presented = RefreshLink.LegacyOf(refreshToken);
            await families.AdoptAsync(
                user.Id,
                presented,
                ticket.Properties.ExpiresUtc!.Value,
                cancellationToken);
        }

        var rotation = await families.RotateAsync(user.Id, presented, cancellationToken);
        if (rotation.IsReplay)
        {
            metrics.RecordSessionsRevoked(SecurityMetrics.RefreshReplayed);
            LogReplayed(logger, user.Id);
        }

        return rotation.Next;
    }

    [LoggerMessage(
        EventName = "accounts.refresh.replayed",
        Level = LogLevel.Warning,
        Message = "A spent refresh token of account {AccountId} came back: its family is"
            + " revoked.")]
    private static partial void LogReplayed(ILogger logger, int accountId);
}
