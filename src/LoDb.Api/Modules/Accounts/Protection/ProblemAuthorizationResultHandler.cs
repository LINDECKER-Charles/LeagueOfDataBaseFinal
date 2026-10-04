using LoDb.Api.Hosting;
using LoDb.Api.Modules.Accounts.Authentication;
using LoDb.Api.Modules.Accounts.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization.Policy;

namespace LoDb.Api.Modules.Accounts.Protection;

/// <summary>
/// Answers a refused <c>/api</c> request with a ProblemDetails whose <c>code</c> says why:
/// forged, anonymous, e-mail not verified, second factor missing, or forbidden.
/// </summary>
/// <remarks>
/// A forgery verdict comes first, anonymous or not: an anonymous request would otherwise
/// read as a mere challenge. The other paths keep the default answers.
/// </remarks>
internal sealed class ProblemAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded || !context.Request.Path.StartsWithSegments(ApiPaths.App))
        {
            await _default.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        if (AccessDenials.Recorded(context) is { } forgery)
        {
            await AccountProblem.Denied(forgery).ExecuteAsync(context);
            return;
        }

        if (authorizeResult.Challenged)
        {
            // The bearer handler adds its WWW-Authenticate header.
            await context.ChallengeAsync();
            await AccountProblem.AuthenticationRequired().ExecuteAsync(context);
            return;
        }

        await AccountProblem.Denied(Reason(authorizeResult.AuthorizationFailure))
            .ExecuteAsync(context);
    }

    private static string Reason(AuthorizationFailure? failure)
    {
        if (failure is null)
        {
            return AccessDenials.Forbidden;
        }

        if (failure.FailureReasons.Any(static reason =>
                reason.Message == AccessDenials.EmailNotVerified))
        {
            return AccessDenials.EmailNotVerified;
        }

        // An administrator without the second factor, not anyone without the role.
        var failed = failure.FailedRequirements.ToList();
        return !failed.OfType<RolesAuthorizationRequirement>().Any()
            && failed.OfType<ClaimsAuthorizationRequirement>().Any(IsMultiFactor)
            ? AccessDenials.MfaRequired
            : AccessDenials.Forbidden;
    }

    private static bool IsMultiFactor(ClaimsAuthorizationRequirement requirement) =>
        requirement.ClaimType == AuthenticationMethods.ClaimType;
}
