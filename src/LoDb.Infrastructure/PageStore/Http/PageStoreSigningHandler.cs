using LoDb.Infrastructure.PageStore.Signing;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.PageStore.Http;

/// <summary>
/// Signs every request of the <c>page-store</c> client with the configured keys, last before
/// the wire: no handler can change a header after the signature covers it.
/// </summary>
internal sealed class PageStoreSigningHandler(
    IOptions<PageStoreOptions> options,
    TimeProvider clock) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var payloadHash = request.Content is null
            ? SigV4Signer.EmptyPayloadHash
            : SigV4Signer.Hash(await request.Content.ReadAsByteArrayAsync(cancellationToken));
        Signer().Sign(request, payloadHash, clock.GetUtcNow());
        return await base.SendAsync(request, cancellationToken);
    }

    // The store is written asynchronously only; an unsigned synchronous request never leaves.
    protected override HttpResponseMessage Send(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("The page-store client sends asynchronously only.");

    private SigV4Signer Signer()
    {
        var settings = options.Value;
        var credentials = new SigV4Credentials(
            settings.AccessKey ?? string.Empty,
            settings.SecretKey ?? string.Empty);
        return new SigV4Signer(credentials, settings.Region);
    }
}
