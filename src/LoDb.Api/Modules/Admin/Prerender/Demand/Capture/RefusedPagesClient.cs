using System.Globalization;
using System.Net;
using System.Text.Json;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Capture;

/// <summary>
/// Reads the visitors' SSR's <c>GET /refused</c> on the stack network, through the
/// <c>prerender-refused</c> client (<see cref="DemandRegistration"/>): a few seconds at most,
/// a bounded answer, no redirect, no cookie, no log line.
/// </summary>
/// <remarks>
/// Any failure (unreachable, slow, too large, not the expected JSON) is a missing read, which
/// the next tick makes again from the same cursor: never an exception.
/// </remarks>
internal sealed class RefusedPagesClient(IHttpClientFactory clients) : IRefusedPagesReader
{
    public const string ClientName = "prerender-refused";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<RefusedPagesReport?> ReadAsync(
        Uri url,
        long after,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);
        try
        {
            using var response = await clients.CreateClient(ClientName)
                .GetAsync(WithCursor(url, after), cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return null;
            }

            var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return JsonSerializer.Deserialize<RefusedPagesReport>(body, Json);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested
            && exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            return null;
        }
    }

    private static Uri WithCursor(Uri url, long after) =>
        new UriBuilder(url) { Query = "after=" + after.ToString(CultureInfo.InvariantCulture) }.Uri;
}
