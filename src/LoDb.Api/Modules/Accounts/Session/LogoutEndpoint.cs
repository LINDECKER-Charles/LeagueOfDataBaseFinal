using System.Security.Claims;
using LoDb.Api.Hosting;
using LoDb.Api.Modules.Accounts.Authentication;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Accounts.Session;

/// <summary>
/// <c>POST /api/account/logout</c>: closes the session cookie and issues the XSRF token of
/// the anonymous page.
/// </summary>
/// <remarks>
/// The tokens of an app are not stored anywhere to be revoked: the app forgets them, and the
/// access token runs out within minutes. A password change or a ban revokes the refresh
/// tokens, through the security stamp.
/// </remarks>
internal sealed class LogoutEndpoint(
    SignInManager<User> signIn,
    XsrfTokens xsrf,
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
            return TypedResults.NoContent();
        }

        await signIn.SignOutAsync();
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
        xsrf.Issue(context);
        return TypedResults.NoContent();
    }
}
