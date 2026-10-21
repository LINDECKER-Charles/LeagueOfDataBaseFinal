using System.Collections.Frozen;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace LoDb.Infrastructure.PageStore.Signing;

/// <summary>
/// The canonical request of Signature Version 4 for an object path without query: the text
/// whose hash is signed, and the list of the headers it covers.
/// </summary>
/// <remarks>
/// Every header the request carries is signed, except those the HTTP stack may add, drop or
/// rewrite on its way out: a signed header the store does not receive as signed fails the
/// signature, an unsigned one could be altered in transit.
/// </remarks>
internal sealed partial record SigV4CanonicalRequest(string Text, string SignedHeaders)
{
    private const string HostHeader = "host";
    private const char HeaderSeparator = ';';
    private const char ValueSeparator = ',';

    private static readonly FrozenSet<string> UnsignedHeaders = FrozenSet.Create(
        StringComparer.Ordinal,
        "authorization",
        "connection",
        "content-length",
        "expect",
        "transfer-encoding",
        "user-agent");

    public static SigV4CanonicalRequest Of(HttpRequestMessage request, string payloadHash)
    {
        var url = request.RequestUri;
        if (url is not { IsAbsoluteUri: true } || url.Query.Length > 0)
        {
            throw new ArgumentException(
                "Only an absolute object URL without query is signed.",
                nameof(request));
        }

        var headers = HeadersOf(request, url);
        var signedHeaders = string.Join(HeaderSeparator, headers.Keys);
        var lines = new StringBuilder();
        foreach (var (name, value) in headers)
        {
            lines.Append(name).Append(':').Append(value).Append('\n');
        }

        var text = string.Join(
            '\n',
            request.Method.Method,
            SigV4Encoding.CanonicalPath(url.AbsolutePath),
            string.Empty,
            lines.ToString(),
            signedHeaders,
            payloadHash);
        return new SigV4CanonicalRequest(text, signedHeaders);
    }

    // Sorted by lower-case name; an explicit Host header wins over the URL's authority.
    private static SortedDictionary<string, string> HeadersOf(HttpRequestMessage request, Uri url)
    {
        var headers = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            [HostHeader] = url.IsDefaultPort
                ? url.IdnHost
                : string.Create(CultureInfo.InvariantCulture, $"{url.IdnHost}:{url.Port}"),
        };
        Add(headers, request.Headers.NonValidated);
        if (request.Content is not null)
        {
            Add(headers, request.Content.Headers.NonValidated);
        }

        return headers;
    }

    private static void Add(
        SortedDictionary<string, string> headers,
        HttpHeadersNonValidated source)
    {
        foreach (var (name, values) in source)
        {
            var key = name.ToLowerInvariant();
            if (!UnsignedHeaders.Contains(key))
            {
                headers[key] = string.Join(ValueSeparator, values.Select(Canonical));
            }
        }
    }

    private static string Canonical(string value) => Whitespace().Replace(value.Trim(), " ");

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
