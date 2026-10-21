using System.Net;

namespace LoDb.Infrastructure.PageStore.Errors;

/// <summary>
/// A failure of the store's configuration, not of an object: the store does not know the key
/// (401), refuses the writer (403 on a write), has no such bucket (404 on a write), redirects
/// (wrong endpoint or region), cannot honour what every request carries (501, 505), or the
/// client was pointed off its allow-list.
/// </summary>
/// <remarks>
/// Every later write would fail alike (rotated key, changed VPS address, policy without the
/// writer): the pass pauses with one log line, and counts the page neither written nor skipped.
/// </remarks>
public sealed class PageStoreConfigurationException(
    string message,
    HttpStatusCode? statusCode = null) : PageStoreException(message, statusCode);
