using LoDb.Api.Hosting;
using LoDb.Api.Modules.Accounts.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace LoDb.Api.Modules.Accounts.Protection;

/// <summary>
/// Checks <see cref="ForgeryRequirement"/>: the <c>Origin</c> of every unsafe <c>/api</c>
/// request, and the XSRF token of those a session cookie authenticates.
/// </summary>
/// <remarks>
/// A request a bearer token authenticated is exempt: a browser never attaches one on its own,
/// and CORS keeps foreign pages from setting the header. The exemption follows the scheme
/// that authenticated the request, not the header: a policy bound to the session cookie
/// authenticates the cookie of a request that also carries a bearer header, and such a
/// request is guarded as any other cookie request. A bearer header that authenticated
/// nothing exempts nothing either. Anonymous requests only have their origin checked: they
/// carry no session to abuse.
/// </remarks>
internal sealed class ForgeryHandler(TrustedOrigins origins, XsrfTokens xsrf)
    : AuthorizationHandler<ForgeryRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ForgeryRequirement requirement)
    {
        // Outside of a request (a direct authorization call) there is nothing to forge.
        if (context.Resource is not HttpContext http || await CheckAsync(http) is not { } denial)
        {
            context.Succeed(requirement);
            return;
        }

        AccessDenials.Record(http, denial);
        context.Fail(new AuthorizationFailureReason(this, denial));
    }

    private async Task<string?> CheckAsync(HttpContext context)
    {
        var request = context.Request;
        if (IsSafe(request.Method) || !request.Path.StartsWithSegments(ApiPaths.App))
        {
            return null;
        }

        var isSignedIn = context.User.Identity?.IsAuthenticated == true;
        if (isSignedIn && IsByBearerOnly(context))
        {
            return null;
        }

        if (!origins.Allows(request))
        {
            return AccessDenials.OriginMismatch;
        }

        return isSignedIn && !await xsrf.IsValidAsync(context) ? AccessDenials.XsrfInvalid : null;
    }

    // The default scheme forwards a bearer request to the bearer handler alone; the cookie
    // only counts when a policy authenticated it as well.
    private static bool IsByBearerOnly(HttpContext context) =>
        AccountSchemes.IsBearer(context.Request) && !SessionAuthentication.IsUsed(context);

    private static bool IsSafe(string method) =>
        HttpMethods.IsGet(method)
        || HttpMethods.IsHead(method)
        || HttpMethods.IsOptions(method)
        || HttpMethods.IsTrace(method);
}
