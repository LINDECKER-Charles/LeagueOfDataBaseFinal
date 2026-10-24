namespace LoDb.Infrastructure.Robots.Reports.Views;

/// <summary>Hits, and how they were verified and answered: of one robot, or of them all.</summary>
public sealed record RobotFigures
{
    public required long Hits { get; init; }

    public required long Verified { get; init; }

    /// <summary>Hits whose user agent most likely borrows a robot's name.</summary>
    public required long Mismatch { get; init; }

    public required long Unresolved { get; init; }

    /// <summary>Answered 2xx.</summary>
    public required long Success { get; init; }

    /// <summary>Answered 3xx.</summary>
    public required long Redirects { get; init; }

    /// <summary>Answered 4xx.</summary>
    public required long ClientErrors { get; init; }

    /// <summary>Answered 5xx.</summary>
    public required long ServerErrors { get; init; }

    /// <summary>
    /// Answered 403, 429 or 503: a guard, a rate limit or the shedding of renders.
    /// </summary>
    public required long Refused { get; init; }

    public required long Bytes { get; init; }

    /// <summary>The mean time nginx took to answer, in milliseconds.</summary>
    public required double MeanMs { get; init; }

    /// <summary>Hits answered in a second or more.</summary>
    public required long SlowHits { get; init; }
}
