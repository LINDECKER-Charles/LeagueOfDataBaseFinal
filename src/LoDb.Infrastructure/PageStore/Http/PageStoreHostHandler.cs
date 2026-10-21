using Microsoft.Extensions.Options;
using LoDb.Infrastructure.PageStore.Errors;

namespace LoDb.Infrastructure.PageStore.Http;

/// <summary>
/// The allow-list of the <c>page-store</c> client: the configured endpoint's scheme, host and
/// port, and object paths of the configured bucket alone. Anything else is refused before it
/// leaves the process, so a signature never goes elsewhere.
/// </summary>
/// <remarks>
/// Its own list, not <c>LoDb:Egress:AllowedHosts</c>: widening the ingestion's list would let
/// Data Dragon's redirects reach the store. A refused destination is a configuration error:
/// every later request of the same caller would go to the same wrong place.
/// </remarks>
internal sealed class PageStoreHostHandler(IOptions<PageStoreOptions> options) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Enforce(request.RequestUri);
        return base.SendAsync(request, cancellationToken);
    }

    // The synchronous path is guarded too: overriding only SendAsync would leave a bypass.
    protected override HttpResponseMessage Send(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Enforce(request.RequestUri);
        return base.Send(request, cancellationToken);
    }

    private void Enforce(Uri? url)
    {
        if (!IsAllowed(url, options.Value))
        {
            throw new PageStoreConfigurationException(
                "The page-store client only reaches the configured bucket.");
        }
    }

    private static bool IsAllowed(Uri? url, PageStoreOptions settings)
    {
        if (url is not { IsAbsoluteUri: true }
            || url.Query.Length > 0
            || url.Fragment.Length > 0
            || string.IsNullOrWhiteSpace(settings.Bucket)
            || !Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out var endpoint))
        {
            return false;
        }

        return string.Equals(url.Scheme, endpoint.Scheme, StringComparison.Ordinal)
            && string.Equals(url.IdnHost, endpoint.IdnHost, StringComparison.OrdinalIgnoreCase)
            && url.Port == endpoint.Port
            && url.AbsolutePath.StartsWith($"/{settings.Bucket}/", StringComparison.Ordinal);
    }
}
