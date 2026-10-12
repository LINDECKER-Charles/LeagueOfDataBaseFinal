using System.Net;

namespace LoDb.Api.Modules.Seo.IndexNow;

/// <summary>
/// Posts a submission to the shared IndexNow endpoint, which hands it to every engine of the
/// protocol.
/// </summary>
/// <remarks>
/// A client of its own, never the <c>ddragon</c> one: a single host, no redirect followed, no
/// cookie kept, a bounded wait (<see cref="SeoModule"/>).
/// </remarks>
internal sealed class IndexNowClient(IHttpClientFactory clients)
{
    public const string ClientName = "indexnow";

    /// <summary>The protocol's cap of URLs per request.</summary>
    public const int MaxUrls = 10_000;

    /// <summary>The wait for one answer.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static readonly Uri Endpoint = new("https://api.indexnow.org/indexnow");

    /// <summary>The status of the answer: 200 or 202 when the submission is accepted.</summary>
    /// <exception cref="HttpRequestException">The endpoint could not be reached.</exception>
    /// <exception cref="TaskCanceledException">The endpoint did not answer in time.</exception>
    public async Task<HttpStatusCode> SubmitAsync(
        IndexNowSubmission submission,
        CancellationToken cancellationToken)
    {
        var client = clients.CreateClient(ClientName);
        using var response = await client.PostAsJsonAsync(Endpoint, submission, cancellationToken);
        return response.StatusCode;
    }
}
