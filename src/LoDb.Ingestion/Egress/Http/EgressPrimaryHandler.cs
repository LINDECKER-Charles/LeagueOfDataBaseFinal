using System.Net;

namespace LoDb.Ingestion.Egress.Http;

/// <summary>
/// The socket handler under the <c>ddragon</c> client.
/// </summary>
internal static class EgressPrimaryHandler
{
    public static SocketsHttpHandler Create(EgressOptions options) =>
        new()
        {
            // RedirectHandler follows them, re-checking every hop.
            AllowAutoRedirect = false,

            // The image CDN only speaks HTTP/1.1: one connection per in-flight fetch.
            MaxConnectionsPerServer = options.FetchConcurrency,

            // The body cap then counts decompressed bytes, which is what reaches the heap.
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = false,
        };
}
