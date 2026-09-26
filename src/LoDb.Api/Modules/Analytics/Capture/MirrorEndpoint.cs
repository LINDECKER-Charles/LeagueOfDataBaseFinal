using LoDb.Infrastructure.Persistence.Analytics;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Analytics.Capture;

/// <summary>
/// <c>POST /internal/analytics/page</c>: nginx's mirror of every page it serves, from the
/// cache or not, the page's address in <c>X-Original-URI</c>. The first page of a visit is
/// counted here; the router's beacon counts the next ones.
/// </summary>
/// <remarks>
/// nginx drops the answer and gives up after a second: it only waits for the enqueuing.
/// The proxy strips the cookies and the credentials; the client's address, user agent and
/// referrer come as they do for the page.
/// </remarks>
internal static class MirrorEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(CaptureRoutes.MirrorPath, Capture)
            .AllowAnonymous()
            .ExcludeFromDescription();

    private static IResult Capture(HttpContext context, [FromServices] ViewIntake intake)
    {
        var header = context.Request.Headers[CaptureRoutes.OriginalUriHeader];
        var target = header.Count == 1 ? header[0] : null;
        if (!CaptureRoutes.IsTarget(target))
        {
            return TypedResults.BadRequest();
        }

        intake.Take(context, target, AnalyticsCaptureOrigin.ServedPage);
        return TypedResults.Accepted(uri: default(string));
    }
}
