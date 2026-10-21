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

    // The addresses and when they were resolved, swapped as one reference: every page in
    // flight reads it outside the semaphore, where a long? would tear.
    private Resolution? _resolution;

    /// <summary>The renderers' addresses now; empty when the pass asks the name itself.</summary>
    public async Task<IReadOnlyList<IPAddress>> CurrentAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.SpreadsRenders || HostOf(options.Value) is not { } host)
        {
            return [];
        }

        if (FreshOrNull() is { } fresh)
        {
            return fresh.Addresses;
        }

        await _resolving.WaitAsync(cancellationToken);
        try
        {
            return (FreshOrNull() ?? await ResolveAsync(host, cancellationToken)).Addresses;
        }
        finally
        {
            _resolving.Release();
        }
    }

    public void Dispose() => _resolving.Dispose();

    private async Task<Resolution> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        var resolved = await dns.ResolveAsync(host, cancellationToken);
        var known = Volatile.Read(ref _resolution)?.Addresses ?? [];
        var resolution = new Resolution(
            resolved.Count > 0 ? resolved : known,
            clock.GetTimestamp());
        Volatile.Write(ref _resolution, resolution);
        return resolution;
    }

    private Resolution? FreshOrNull() =>
        Volatile.Read(ref _resolution) is { } resolution
            && clock.GetElapsedTime(resolution.At) < Refresh
            ? resolution
            : null;

    // An address already needs no resolution, nor any spread.
    private static string? HostOf(PrerenderOptions options) =>
        Uri.TryCreate(options.SsrBaseUrl, UriKind.Absolute, out var url)
            && url.HostNameType == UriHostNameType.Dns
            ? url.Host
            : null;

    private sealed record Resolution(IReadOnlyList<IPAddress> Addresses, long At);
}
