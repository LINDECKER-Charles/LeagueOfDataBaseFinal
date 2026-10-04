using System.Net;
using LoDb.Infrastructure.Persistence.Analytics;

namespace LoDb.Infrastructure.Analytics.Capture;

/// <summary>
/// A view as a capture endpoint took it in: the request's facts only, the page and the
/// client being resolved later, off the request.
/// </summary>
public sealed record CapturedView
{
    public required DateTimeOffset OccurredAt { get; init; }

    public required AnalyticsCaptureOrigin Origin { get; init; }

    /// <summary>The page's path and query, as requested.</summary>
    public required string Target { get; init; }

    /// <summary>Host the page was requested on, without its port.</summary>
    public required string Host { get; init; }

    /// <summary>The client's real address, as the proxies forwarded it.</summary>
    public IPAddress? ClientAddress { get; init; }

    public string? UserAgent { get; init; }

    /// <summary>
    /// The page's referrer; ignored for an internal navigation, which the site itself led to.
    /// </summary>
    public string? Referer { get; init; }
}
