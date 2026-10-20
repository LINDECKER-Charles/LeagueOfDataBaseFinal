using System.Net;

namespace LoDb.Infrastructure.PageStore.Errors;

/// <summary>
/// A refusal of this one object, which a retry repeats: the client refused to send it
/// (metadata over the limit, a header value that does not fit on one line), or the store
/// answered another 4xx (a 400 on a malformed header, a 412 on a write that is not
/// create-only…).
/// </summary>
/// <remarks>
/// It points at the object, never at the store's health nor at its configuration (that is a
/// <see cref="PageStoreConfigurationException"/>): the pass counts the page as failed and goes
/// on with the next one.
/// </remarks>
public sealed class PageStoreRefusedException(string message, HttpStatusCode? statusCode = null)
    : PageStoreException(message, statusCode);
