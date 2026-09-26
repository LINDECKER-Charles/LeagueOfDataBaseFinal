using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Modules.Accounts.Protection;

/// <summary>
/// Checks <see cref="VerifiedEmailRequirement"/> against the account row: a verification
/// made in another tab counts at once, without waiting for the session to be revalidated.
/// </summary>
/// <remarks>
/// Authorization builds every handler for every authorized request, the public API's
/// included: the user manager, and the database context behind it, are only resolved by
/// the requests that need them.
/// </remarks>
internal sealed class VerifiedEmailHandler(IServiceProvider services)
    : AuthorizationHandler<VerifiedEmailRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        VerifiedEmailRequirement requirement)
    {
        // An anonymous request fails on its authentication requirement, answered with 401.
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var users = services.GetRequiredService<UserManager<User>>();
        var user = await users.GetUserAsync(context.User);
        if (user is { EmailConfirmed: true, IsBanned: false })
        {
            context.Succeed(requirement);
            return;
        }

        // A banned account whose session is not revalidated yet is merely refused.
        var reason = user is { IsBanned: false }
            ? AccessDenials.EmailNotVerified
            : AccessDenials.Forbidden;
        context.Fail(new AuthorizationFailureReason(this, reason));
    }
}
