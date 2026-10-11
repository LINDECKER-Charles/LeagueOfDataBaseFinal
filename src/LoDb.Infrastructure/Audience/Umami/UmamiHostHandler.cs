using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Audience.Umami;

/// <summary>
/// The allow-list of the <c>umami</c> client: https, and the one configured host
/// (<see cref="AudienceOptions.Host"/>). Anything else is refused before it leaves the process,
/// so the read-only account's credentials never go elsewhere.
/// </summary>
internal sealed class UmamiHostHandler(IOptions<AudienceOptions> options) : DelegatingHandler
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
        var host = options.Value.Host;
        if (url is not { IsAbsoluteUri: true }
            || !string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(host)
            || !string.Equals(url.IdnHost, host, StringComparison.OrdinalIgnoreCase))
        {
            throw new UmamiException("The umami client only reaches the configured https host.");
        }
    }
}
