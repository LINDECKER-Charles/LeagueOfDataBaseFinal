using LoDb.Infrastructure.Analytics.Capture;
using LoDb.Infrastructure.Persistence.Analytics;
using Microsoft.Extensions.Primitives;

namespace LoDb.Api.Modules.Analytics.Capture;

/// <summary>
/// Hands a view to the capture queue with the request's facts, and nothing more: the page
/// and the client are resolved later, by the writer, so that a view costs the request no
/// lookup and no I/O.
/// </summary>
internal sealed class ViewIntake(IPageViewCapture capture, TimeProvider time)
{
    /// <param name="context">The request that reported the view.</param>
    /// <param name="target">The page's path and query.</param>
    /// <param name="origin">Served by nginx, or navigated to inside the application.</param>
    public void Take(HttpContext context, string target, AnalyticsCaptureOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(context);
        var request = context.Request;
        capture.TryCapture(new CapturedView
        {
            OccurredAt = time.GetUtcNow(),
            Origin = origin,
            Target = target,
            Host = request.Host.Host,
            ClientAddress = context.Connection.RemoteIpAddress,
            UserAgent = FirstOf(request.Headers.UserAgent),
            Referer = FirstOf(request.Headers.Referer),
        });
    }

    // The first value, as the legacy stack read its headers.
    private static string? FirstOf(StringValues values) => values.Count == 0 ? null : values[0];
}
