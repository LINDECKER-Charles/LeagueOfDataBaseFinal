using System.Net;
using LoDb.Ingestion.Egress.Errors;
using LoDb.Ingestion.Egress.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace LoDb.Ingestion.Egress;

/// <summary>
/// Sends a GET through the <c>ddragon</c> client and turns the answer into a verdict:
/// 2xx is <see cref="FetchOutcome.Present"/>, 403/404 is <see cref="FetchOutcome.Absent"/>,
/// anything else an <see cref="EgressException"/>.
/// </summary>
internal sealed class EgressFetcher(
    IHttpClientFactory clientFactory,
    AllowList allowList,
    IOptions<EgressOptions> options,
    TimeProvider timeProvider,
    ILogger<EgressFetcher> logger) : IEgressFetcher
{
    public async Task<FetchOutcome> FetchAsync(Uri url, CancellationToken cancellationToken)
    {
        using var batch = OpenBatch();
        return await batch.FetchAsync(url, cancellationToken).ConfigureAwait(false);
    }

    public IEgressBatch OpenBatch() => new EgressBatch(this, logger);

    internal async Task<FetchOutcome> SendAsync(Uri url, CancellationToken cancellationToken)
    {
        // Checked here as well so a relative URL is refused, not rejected by HttpClient.
        allowList.Enforce(url);
        var response = await GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            return await PresentAsync(response, cancellationToken).ConfigureAwait(false);
        }

        var status = response.StatusCode;
        response.Dispose();
        return status is HttpStatusCode.Forbidden or HttpStatusCode.NotFound
            ? new FetchOutcome.Absent((int)status)
            : throw new EgressTransientException($"Upstream answered {(int)status}.", status);
    }

    private async Task<HttpResponseMessage> GetAsync(Uri url, CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient(EgressRegistration.ClientName);
        try
        {
            return await client
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (IsTransport(exception, cancellationToken))
        {
            throw new EgressTransientException("Upstream unreachable.", exception);
        }
    }

    private async Task<FetchOutcome> PresentAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        try
        {
            if (response.Content.Headers.ContentLength > settings.MaxResponseBytes)
            {
                throw new EgressBodyTooLargeException(settings.MaxResponseBytes);
            }

            var body = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            var policy = new BodyPolicy(
                settings.MaxResponseBytes, settings.AttemptTimeout, timeProvider);
            return new FetchOutcome.Present(
                new BoundedResponseStream(response, body, policy),
                response.Content.Headers.ContentType?.ToString());
        }
        catch (Exception exception)
        {
            response.Dispose();
            throw IsTransport(exception, cancellationToken)
                ? new EgressTransientException("Response body unreadable.", exception)
                : exception;
        }
    }

    // Timeouts and open circuits (Polly rejections), network failures, and a cancellation
    // the caller did not ask for. Egress exceptions already carry their verdict.
    private static bool IsTransport(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException or ExecutionRejectedException or IOException
        || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);
}
