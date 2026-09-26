using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Accounts.Protection;
using LoDb.Infrastructure.Persistence.Analytics;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Analytics.Capture;

/// <summary>
/// <c>POST /api/analytics/view</c>: the router's beacon, at the end of each navigation inside
/// the application, which nginx never sees.
/// </summary>
/// <remarks>
/// <para>
/// Anonymous and exempt from the XSRF token, which <c>navigator.sendBeacon</c> cannot send:
/// it records a view, never an action of the session. Its origin is checked as for any
/// unsafe request, and each address gets <see cref="BeaconRateLimit"/> views a minute.
/// </para>
/// <para>
/// Called by the browser alone, out of the generated client: kept out of the contract.
/// </para>
/// </remarks>
internal static class BeaconEndpoint
{
    private const string PathField = "path";
    private const string InvalidPath = "invalid-path";

    public static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(CaptureRoutes.BeaconPath, CaptureAsync)
            .AllowAnonymous()
            .RequireRateLimiting(CaptureRoutes.BeaconPolicy)
            .ExcludeFromDescription();

    private static async Task<IResult> CaptureAsync(
        HttpContext context,
        [FromServices] ViewIntake intake,
        [FromServices] TrustedOrigins origins)
    {
        if (!origins.Allows(context.Request))
        {
            return AccountProblem.Denied(AccessDenials.OriginMismatch);
        }

        var path = await BeaconBody.ReadPathAsync(context.Request, context.RequestAborted);
        if (!CaptureRoutes.IsTarget(path))
        {
            var errors = new FieldErrors();
            errors.Add(PathField, InvalidPath);
            return errors.ToProblem();
        }

        intake.Take(context, path, AnalyticsCaptureOrigin.Navigation);
        return TypedResults.Accepted(uri: default(string));
    }
}
