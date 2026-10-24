using LoDb.Infrastructure.Robots.Capture;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Analytics.Capture;

/// <summary>
/// <c>POST /internal/robots/journal</c>: what nginx reports of the requests it answered
/// (docker/nginx/njs/robot-journal.js), one JSON object a line, every two seconds.
/// </summary>
/// <remarks>
/// <para>
/// Outside of <c>/api</c>, which nginx routes to the API: no client reaches it, and the
/// network gate answers 404 to any caller off the stack's network. Anonymous as the mirror
/// is, since nginx holds no session: the third capture of the list
/// <c>AnonymousEndpointsTests</c> keeps closed (ADR 0013).
/// </para>
/// <para>
/// It only queues the records, without I/O, and answers 202; nginx keeps a batch it could
/// not post and posts it again.
/// </para>
/// </remarks>
internal static class RobotJournalEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(CaptureRoutes.RobotJournalPath, TakeAsync)
            .AllowAnonymous()
            .ExcludeFromDescription();

    private static async Task<IResult> TakeAsync(
        HttpContext context,
        [FromServices] IRobotJournal journal)
    {
        var body = await JournalBody.ReadAsync(context.Request, context.RequestAborted);
        if (body is null)
        {
            return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var (records, unreadable) = JournalLines.Parse(body.Value.Span);
        journal.Take(records, unreadable);
        return TypedResults.Accepted(uri: default(string));
    }
}
