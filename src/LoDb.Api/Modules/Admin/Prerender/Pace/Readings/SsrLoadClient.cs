using System.Net;
using System.Text.Json;
using LoDb.Api.Modules.Admin.Prerender.Pace.Readings.Load;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Readings;

/// <summary>
/// Reads an SSR's <c>GET /load</c> on the stack network, through the <c>prerender-load</c>
/// client: two seconds at most, 64 KiB at most, no redirect, no cookie, no log line.
/// </summary>
/// <remarks>
/// Never the SSR's <c>/metrics</c>: prom-client resets its event loop histogram at each
/// collect, and a second reader would cut the series the host's collector reads short. Any
/// failure (unreachable, slow, too large, not the expected JSON) is a missing reading, which
/// slows the pass: never an exception.
/// </remarks>
internal sealed class SsrLoadClient(IHttpClientFactory clients) : ISsrLoadReader
{
    public const string ClientName = "prerender-load";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<SsrLoad?> ReadAsync(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await clients.CreateClient(ClientName)
                .GetAsync(url, cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return null;
            }

            var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return JsonSerializer.Deserialize<SsrLoad>(body, Json);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested
            && exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            return null;
        }
    }
}
