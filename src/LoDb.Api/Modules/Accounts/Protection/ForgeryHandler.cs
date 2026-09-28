using LoDb.Api.Hosting;
using LoDb.Api.Modules.Accounts.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace LoDb.Api.Modules.Accounts.Protection;

/// <summary>
/// Checks <see cref="ForgeryRequirement"/>: the <c>Origin</c> of every unsafe <c>/api</c>
/// request, and the XSRF token of those a session cookie authenticates.
/// </summary>
/// <remarks>
/// A request authenticated by bearer token is exempt: a browser never attaches one on its
/// own, and CORS keeps foreign pages from setting the header. Anonymous requests only have
/// their origin checked: they carry no session to abuse.
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
        if (IsExempt(request))
        {
            return null;
        }

        if (!origins.Allows(request))
        {
            return AccessDenials.OriginMismatch;
        }

        var bySession = context.User.Identity?.IsAuthenticated == true;
        return bySession && !await xsrf.IsValidAsync(context) ? AccessDenials.XsrfInvalid : null;
    }

    private static bool IsExempt(HttpRequest request) =>
        IsSafe(request.Method)
        || !request.Path.StartsWithSegments(ApiPaths.App)
        || AccountSchemes.IsBearer(request);

    private static bool IsSafe(string method) =>
        HttpMethods.IsGet(method)
        || HttpMethods.IsHead(method)
        || HttpMethods.IsOptions(method)
        || HttpMethods.IsTrace(method);
}
