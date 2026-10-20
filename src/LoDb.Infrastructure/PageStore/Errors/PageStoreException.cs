using System.Net;

namespace LoDb.Infrastructure.PageStore.Errors;

/// <summary>
/// A store operation that produced neither a write nor an answer about an object.
/// </summary>
/// <remarks>
/// The message names the operation, the key, the status and the store's error code, never
/// a header of the request: the signature carries the access key.
/// </remarks>
public abstract class PageStoreException : Exception
{
    protected PageStoreException(string message, HttpStatusCode? statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }

    protected PageStoreException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>The store's status when it answered, null when it never did.</summary>
    public HttpStatusCode? StatusCode { get; }
}
