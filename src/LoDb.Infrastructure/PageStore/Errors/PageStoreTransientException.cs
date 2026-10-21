using System.Net;

namespace LoDb.Infrastructure.PageStore.Errors;

/// <summary>
/// A failure worth retrying later: 5xx (but 501 and 505), 429 (Scaleway's throttling), 408, a
/// timeout or a transport error, the answer's body included.
/// </summary>
public sealed class PageStoreTransientException : PageStoreException
{
    public PageStoreTransientException(string message, HttpStatusCode statusCode)
        : base(message, statusCode)
    {
    }

    public PageStoreTransientException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
