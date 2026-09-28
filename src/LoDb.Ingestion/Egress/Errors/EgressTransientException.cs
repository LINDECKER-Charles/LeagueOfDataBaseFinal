using System.Net;

namespace LoDb.Ingestion.Egress.Errors;

/// <summary>
/// An upstream outage worth retrying later: 5xx or 408 after the retries, attempt or body
/// timeout, transport failure, open circuit, other unexpected status, redirect loop.
/// </summary>
public sealed class EgressTransientException : EgressException
{
    public EgressTransientException(string message, HttpStatusCode? statusCode = null)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public EgressTransientException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// The last upstream status when the upstream answered, null when it never did.
    /// </summary>
    public HttpStatusCode? StatusCode { get; }
}
