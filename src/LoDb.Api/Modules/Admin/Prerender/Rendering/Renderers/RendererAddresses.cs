using System.Net;
using LoDb.Api.Modules.Admin.Prerender.Options;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Rendering.Renderers;

/// <summary>
/// The addresses of the renderers behind <see cref="PrerenderOptions.SsrBaseUrl"/>, resolved
/// again every <see cref="Refresh"/>: a replica that starts, stops or moves is seen within it.
/// </summary>
/// <remarks>
/// Empty with a single renderer (nothing to spread) and while the name resolves to nothing: the
/// pass then asks the name as configured. A failed resolution keeps the addresses known before
/// it, so that a hiccup of the DNS does not fold every render onto one replica.
/// </remarks>
internal sealed class RendererAddresses(
    IOptions<PrerenderOptions> options,
    IRendererDns dns,
    TimeProvider clock) : IDisposable
{
    /// <summary>How long a resolution holds.</summary>
    public static readonly TimeSpan Refresh = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _resolving = new(1, 1);
    private IReadOnlyList<IPAddress> _addresses = [];
    private long? _resolvedAt;

    /// <summary>The renderers' addresses now; empty when the pass asks the name itself.</summary>
    public async Task<IReadOnlyList<IPAddress>> CurrentAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.SpreadsRenders || HostOf(options.Value) is not { } host)
        {
            return [];
        }

        if (IsFresh())
        {
            return Volatile.Read(ref _addresses);
        }

        await _resolving.WaitAsync(cancellationToken);
        try
        {
            if (!IsFresh())
            {
                await ResolveAsync(host, cancellationToken);
            }

            return _addresses;
        }
        finally
        {
            _resolving.Release();
        }
    }

    private async Task ResolveAsync(string host, CancellationToken cancellationToken)
    {
        var resolved = await dns.ResolveAsync(host, cancellationToken);
        if (resolved.Count > 0)
        {
            Volatile.Write(ref _addresses, resolved);
        }

        _resolvedAt = clock.GetTimestamp();
    }

    public void Dispose() => _resolving.Dispose();

    private bool IsFresh() =>
        _resolvedAt is { } at && clock.GetElapsedTime(at) < Refresh;

    // An address already needs no resolution, nor any spread.
    private static string? HostOf(PrerenderOptions options) =>
        Uri.TryCreate(options.SsrBaseUrl, UriKind.Absolute, out var url)
            && url.HostNameType == UriHostNameType.Dns
            ? url.Host
            : null;
}
