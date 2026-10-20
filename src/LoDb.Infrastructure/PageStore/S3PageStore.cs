using System.Net;
using LoDb.Infrastructure.PageStore.Errors;
using LoDb.Infrastructure.PageStore.Http;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.PageStore;

/// <summary>
/// The page store over the S3 API (Scaleway in production, MinIO in development and tests),
/// through the <c>page-store</c> client, which pins the endpoint and signs each request.
/// </summary>
/// <remarks>
/// No retry here: a transient failure surfaces at once, and the pass decides when to come back
/// to the page. Nothing is logged either; the pass writes one summary line per unit.
/// </remarks>
internal sealed class S3PageStore(
    IHttpClientFactory clients,
    IOptions<PageStoreOptions> options) : IPageStore
{
    public async Task<PageWriteOutcome> PutAsync(
        PageObject page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        using var request = PageStoreRequests.Put(Settings(), page);
        using var response = await SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return PageWriteOutcome.Written;
        }

        return page.IsCreateOnly && response.StatusCode == HttpStatusCode.PreconditionFailed
            ? PageWriteOutcome.AlreadyPresent
            : throw await PageStoreResponses.FailureAsync(request, response, cancellationToken);
    }

    public async Task<StoredPage?> HeadAsync(string key, CancellationToken cancellationToken)
    {
        using var request = PageStoreRequests.Head(Settings(), key);
        using var response = await SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return PageStoreResponses.StoredPageOf(key, response);
        }

        return PageStoreResponses.IsAbsence(response.StatusCode)
            ? null
            : throw await PageStoreResponses.FailureAsync(request, response, cancellationToken);
    }

    private PageStoreOptions Settings()
    {
        var settings = options.Value;
        return settings.IsConfigured
            ? settings
            : throw new InvalidOperationException(
                "The page store is not configured (LoDb:Prerender): check IsConfigured first.");
    }

    // The answer is read whole (S3 error bodies are a few hundred bytes), so its body falls
    // within the client's timeout and a reset while reading it is a transport failure too. A
    // timeout of the client is transient; a cancellation by the caller stays one.
    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var what = $"{request.Method} {request.RequestUri?.AbsolutePath}";
        try
        {
            return await clients.CreateClient(PageStoreRegistration.ClientName).SendAsync(
                request,
                HttpCompletionOption.ResponseContentRead,
                cancellationToken);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new PageStoreTransientException($"{what} timed out.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new PageStoreTransientException($"{what} failed in transport.", exception);
        }
    }
}
