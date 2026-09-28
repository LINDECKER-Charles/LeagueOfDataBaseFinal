namespace LoDb.Ingestion.Egress.Http;

/// <summary>
/// Refuses, before it leaves the process, any request of the <c>ddragon</c> client the
/// allow-list does not permit, redirect hops included since they pass through it too.
/// </summary>
internal sealed class AllowListHandler(AllowList allowList) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        allowList.Enforce(request.RequestUri);
        return base.SendAsync(request, cancellationToken);
    }

    // The synchronous path is guarded too: overriding only SendAsync would leave a bypass.
    protected override HttpResponseMessage Send(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        allowList.Enforce(request.RequestUri);
        return base.Send(request, cancellationToken);
    }
}
