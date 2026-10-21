namespace LoDb.Api.Modules.Admin.Prerender.Rendering.Renderers;

/// <summary>
/// Sends each request of the <c>prerender-ssr</c> client to one of the renderers' addresses,
/// the least busy (<see cref="RendererSlots"/>), rather than to their shared name: a client
/// keeps its connections, so the name alone would pin the pass on whichever replica answered
/// first.
/// </summary>
/// <remarks>
/// Only the URL's host changes: the <c>Host</c> header, the public host the client sets on
/// every request, stays as it was. With a single renderer, or a name that resolves to nothing,
/// the request goes as it came.
/// </remarks>
internal sealed class RendererSpreadHandler(RendererAddresses addresses, RendererSlots slots)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var renderers = await addresses.CurrentAsync(cancellationToken);
        if (request.RequestUri is not { IsAbsoluteUri: true } url || renderers.Count == 0)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var renderer = slots.Take(renderers);
        try
        {
            request.RequestUri = new UriBuilder(url) { Host = renderer.ToString() }.Uri;
            return await base.SendAsync(request, cancellationToken);
        }
        finally
        {
            slots.Release(renderer);
        }
    }
}
