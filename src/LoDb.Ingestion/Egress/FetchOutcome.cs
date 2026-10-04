namespace LoDb.Ingestion.Egress;

/// <summary>
/// The result of a fetch that reached a verdict: a body, or a definitive absence.
/// </summary>
/// <remarks>
/// A transient failure is never an outcome: it is an
/// <see cref="Errors.EgressTransientException"/>, so no caller can persist it by mistake.
/// </remarks>
public abstract record FetchOutcome
{
    private FetchOutcome()
    {
    }

    /// <summary>
    /// A 2xx body, streamed. The caller owns <paramref name="Content"/> and must dispose it
    /// promptly: it holds one of the <c>FetchConcurrency</c> connections until then.
    /// </summary>
    /// <param name="Content">
    /// The body, capped: reading past the cap throws
    /// <see cref="Errors.EgressBodyTooLargeException"/>, a stalled or broken read throws
    /// <see cref="Errors.EgressTransientException"/>.
    /// </param>
    /// <param name="ContentType">The Content-Type header, null when absent.</param>
    public sealed record Present(Stream Content, string? ContentType) : FetchOutcome;

    /// <summary>
    /// A 403 or 404: the resource does not exist and never will for this immutable URL.
    /// </summary>
    public sealed record Absent(int StatusCode) : FetchOutcome;
}
