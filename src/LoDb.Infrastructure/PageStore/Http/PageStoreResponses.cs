using System.Net;
using System.Net.Http.Headers;
using System.Text;
using LoDb.Infrastructure.PageStore.Errors;
using Microsoft.Net.Http.Headers;

namespace LoDb.Infrastructure.PageStore.Http;

/// <summary>
/// How the store's answers read: the object a HEAD describes, and the failure a status means.
/// </summary>
internal static class PageStoreResponses
{
    // S3 error bodies are short XML documents; their code is all that is kept.
    private const int MaxErrorBytes = 4 * 1024;
    private const string CodeStart = "<Code>";
    private const string CodeEnd = "</Code>";
    private const int MaxCodeLength = 64;

    /// <summary>403 and 404: the object is not there for this client, and will not be.</summary>
    public static bool IsAbsence(HttpStatusCode status) =>
        status is HttpStatusCode.Forbidden or HttpStatusCode.NotFound;

    /// <summary>The object a successful HEAD describes.</summary>
    public static StoredPage StoredPageOf(string key, HttpResponseMessage response)
    {
        var metadata = response.Headers.NonValidated
            .Where(static header => header.Key.StartsWith(
                PageStoreRequests.MetadataPrefix,
                StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                static header => header.Key[PageStoreRequests.MetadataPrefix.Length..]
                    .ToLowerInvariant(),
                static header => header.Value.ToString(),
                StringComparer.Ordinal);
        var content = response.Content.Headers;
        return new StoredPage
        {
            Key = key,
            ContentLength = content.ContentLength ?? 0,
            ContentType = Value(content, HeaderNames.ContentType),
            ContentEncoding = Value(content, HeaderNames.ContentEncoding),
            CacheControl = Value(response.Headers, HeaderNames.CacheControl),
            StorageClass = Value(response.Headers, PageStoreRequests.StorageClassHeader)
                ?? StoredPage.StandardStorageClass,
            Metadata = metadata,
        };
    }

    /// <summary>
    /// The exception an unexpected answer to <paramref name="request"/> stands for: transient
    /// for 5xx (but 501 and 505), 429 and 408; a configuration error for a redirect, 401, 501,
    /// 505, and 403 or 404 on a write; a refusal of the object otherwise.
    /// </summary>
    /// <remarks>
    /// The body is already buffered (the client reads answers whole, within its timeout), so
    /// reading its code here does no I/O that could hang or fail.
    /// </remarks>
    public static async Task<PageStoreException> FailureAsync(
        HttpRequestMessage request,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var status = response.StatusCode;
        var code = await ErrorCodeAsync(response, cancellationToken);
        var message = $"{request.Method} {request.RequestUri?.AbsolutePath} answered "
            + $"{(int)status}{(code is null ? string.Empty : $" ({code})")}.";
        if (IsTransient(status))
        {
            return new PageStoreTransientException(message, status);
        }

        return IsConfigurationError(request.Method, status)
            ? new PageStoreConfigurationException(message, status)
            : new PageStoreRefusedException(message, status);
    }

    // 501 and 505 say the store cannot do what every request asks (a conditional write, say):
    // retrying would only repeat them.
    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
        || ((int)status >= (int)HttpStatusCode.InternalServerError
            && status is not (HttpStatusCode.NotImplemented
                or HttpStatusCode.HttpVersionNotSupported));

    // A redirect means a wrong endpoint or region, 401 an unknown key; on a write, 403 a policy
    // or an address that no longer lets the writer in, 404 a missing bucket. A HEAD's 403 and
    // 404 are an absence, answered before this.
    private static bool IsConfigurationError(HttpMethod method, HttpStatusCode status) =>
        IsRedirect(status)
        || status is HttpStatusCode.Unauthorized
            or HttpStatusCode.NotImplemented
            or HttpStatusCode.HttpVersionNotSupported
        || (method == HttpMethod.Put
            && status is HttpStatusCode.Forbidden or HttpStatusCode.NotFound);

    private static bool IsRedirect(HttpStatusCode status) =>
        (int)status is >= (int)HttpStatusCode.MultipleChoices and < (int)HttpStatusCode.BadRequest;

    private static string? Value(HttpHeaders headers, string name) =>
        headers.NonValidated.TryGetValues(name, out var values) ? values.ToString() : null;

    // The S3 error code alone (AccessDenied, SlowDown…), and only if it looks like one: the
    // rest of the body is never echoed.
    private static async Task<string?> ErrorCodeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxErrorBytes];
        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        var length = await body.ReadAtLeastAsync(
            buffer,
            MaxErrorBytes,
            throwOnEndOfStream: false,
            cancellationToken);
        var text = Encoding.UTF8.GetString(buffer, 0, length);
        var start = text.IndexOf(CodeStart, StringComparison.Ordinal);
        var end = start < 0 ? -1 : text.IndexOf(CodeEnd, start, StringComparison.Ordinal);
        if (end < 0)
        {
            return null;
        }

        var code = text[(start + CodeStart.Length)..end];
        return code.Length is > 0 and <= MaxCodeLength && code.All(char.IsAsciiLetterOrDigit)
            ? code
            : null;
    }
}
