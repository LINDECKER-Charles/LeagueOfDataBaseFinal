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
/// <c>POST /api/account/logout</c>: closes the session cookie and issues the XSRF token of
/// the anonymous page, or, for an app, revokes the family of its refresh token.
/// </summary>
/// <remarks>
/// The access token of an app is not stored anywhere to be revoked: the app forgets it, and
/// it runs out within minutes. Its refresh token, named in the access token, can no longer
/// be traded once its family is revoked.
/// </remarks>
internal sealed class LogoutEndpoint(
    SignInManager<User> signIn,
    RefreshFamilies families,
    XsrfTokens xsrf,
    SecurityMetrics metrics,
    AccountAudit audit)
{
    public static void Map(IEndpointRouteBuilder account) =>
        account.MapPost(
                "/logout",
                static (
                    [FromServices] LogoutEndpoint endpoint,
                    HttpContext context,
                    CancellationToken aborted) => endpoint.LogoutAsync(context, aborted))
            .RequireAuthorization(AuthorizationPolicies.Authenticated)
            .WithName("signOut")
            .WithSummary("Closes the session.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

    public async Task<NoContent> LogoutAsync(HttpContext context, CancellationToken cancellation)
    {
        // Recorded first: the journal reads the account from the request.
        await audit.SignedOutAsync(cancellation);
        if (AccountSchemes.IsBearer(context.Request))
        {
            await RevokeFamilyAsync(context.User, cancellation);
            return TypedResults.NoContent();
        }

        await signIn.SignOutAsync();
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
        xsrf.Issue(context);
        return TypedResults.NoContent();
    }

    // An access token handed out before the families names none: it runs out on its own.
    private async Task RevokeFamilyAsync(ClaimsPrincipal user, CancellationToken cancellation)
    {
        if (RefreshFamilyClaims.Read(user) is { } link
            && await families.RevokeAsync(link.FamilyId, cancellation) > 0)
        {
            metrics.RecordSessionsRevoked(SecurityMetrics.SignedOut);
        }
    }
}
