using System.Net;
using LoDb.Ingestion.Egress.Errors;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Egress.Http;

/// <summary>
/// Follows redirects by hand, each hop checked against the allow-list first.
/// </summary>
/// <remarks>
/// The primary handler never follows redirects on its own: an allow-listed host answering
/// 302 would otherwise carry the request anywhere, only the first hop being checked.
/// </remarks>
internal sealed class RedirectHandler(AllowList allowList, IOptions<EgressOptions> options)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var maxRedirects = options.Value.MaxRedirects;
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        for (var hops = 0; TargetOf(response, request.RequestUri) is { } target; hops++)
        {
            response.Dispose();
            if (hops == maxRedirects)
            {
                throw new EgressTransientException(
                    $"More than {maxRedirects} redirects.", response.StatusCode);
            }

            // Its own reason: an allow-listed origin relaying us elsewhere is not a caller bug.
            if (allowList.Check(target) is not null)
            {
                throw new EgressRefusedException(EgressRefusal.Redirect, AllowList.HostOf(target));
            }

            request = Follow(request, target);
            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    private static Uri? TargetOf(HttpResponseMessage response, Uri? requested)
    {
        if (!IsRedirect(response.StatusCode) || response.Headers.Location is not { } location)
        {
            return null;
        }

        return location.IsAbsoluteUri || requested is null
            ? location
            : new Uri(requested, location);
    }

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.MovedPermanently
            or HttpStatusCode.Found
            or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;

    // Only GETs leave the client, so every hop is a GET, on the same protocol version.
    private static HttpRequestMessage Follow(HttpRequestMessage previous, Uri target) =>
        new(HttpMethod.Get, target)
        {
            Version = previous.Version,
            VersionPolicy = previous.VersionPolicy,
        };
}
