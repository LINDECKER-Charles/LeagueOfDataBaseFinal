using System.Security.Claims;
using LoDb.Api.Hosting;
using LoDb.Api.Modules.Accounts.Authentication;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Accounts.Security;
using LoDb.Api.Modules.Accounts.Tokens;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Accounts.Session;

/// <summary>
/// <c>POST /api/account/logout-everywhere</c>: closes every session of the account, on every
/// browser and in every app, this one included.
/// </summary>
/// <remarks>
/// <para>
/// A new security stamp ends the other session cookies at their next revalidation
/// (<see cref="AuthenticationSetup.RevalidationInterval"/>) and the refresh tokens at their
/// next use; every refresh token family is revoked as well. The access tokens already
/// handed out run out within minutes.
/// </para>
/// <para>
/// The session of the request closes at once: a cookie is signed out here, and an app,
/// whose tokens the API cannot take back, forgets them as at a sign-out.
/// </para>
/// </remarks>
internal sealed class SignOutEverywhereEndpoint(
    UserManager<User> users,
    SignInManager<User> signIn,
    RefreshFamilies families,
    XsrfTokens xsrf,
    SecurityMetrics metrics,
    AccountAudit audit)
{
    public static void Map(IEndpointRouteBuilder account) =>
        account.MapPost(
                "/logout-everywhere",
                static (
                    [FromServices] SignOutEverywhereEndpoint endpoint,
                    HttpContext context,
                    CancellationToken aborted) => endpoint.SignOutAsync(context, aborted))
            .RequireAuthorization(AuthorizationPolicies.Authenticated)
            .WithName("signOutEverywhere")
            .WithSummary("Closes every session of the account, this one included.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

    public async Task<Results<NoContent, AccountProblem>> SignOutAsync(
        HttpContext context,
        CancellationToken cancellation)
    {
        if (await users.GetUserAsync(context.User) is not { } user)
        {
            return AccountProblem.AuthenticationRequired();
        }

        await RevokeAllAsync(user, cancellation);
        // Recorded once the sessions are revoked, and before the cookie's sign-out: the
        // journal reads the account from the request.
        await audit.SignedOutEverywhereAsync(cancellation);
        if (!AccountSchemes.IsBearer(context.Request))
        {
            await signIn.SignOutAsync();
            context.User = new ClaimsPrincipal(new ClaimsIdentity());
            xsrf.Issue(context);
        }

        return TypedResults.NoContent();
    }

    private async Task RevokeAllAsync(User user, CancellationToken cancellation)
    {
        var stamped = await users.UpdateSecurityStampAsync(user);
        if (!stamped.Succeeded)
        {
            throw new InvalidOperationException(
                $"The security stamp of account {user.Id} could not be renewed: "
                + string.Join(", ", stamped.Errors.Select(static error => error.Code)));
        }

        await families.RevokeAllAsync(user.Id, cancellation);
        metrics.RecordSessionsRevoked(SecurityMetrics.SignedOutEverywhere);
    }
}
