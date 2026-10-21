using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pace.Readings;
using LoDb.Api.Modules.Admin.Prerender.Pace.Readings.Load;
using LoDb.Api.Modules.Admin.Prerender.Rendering.Renderers;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Resources;

/// <summary>
/// Reads the load of the visitors' SSR and of every renderer of the pass at once: with several
/// renderers, each address the pass spreads over, on the port of <c>Pace:PassLoadUrl</c>.
/// </summary>
internal sealed class SsrLoadsReader(
    IOptions<PrerenderOptions> options,
    ISsrLoadReader loads,
    RendererAddresses renderers)
{
    public async Task<SsrLoadSet> ReadAsync(CancellationToken cancellationToken)
    {
        var pace = options.Value.Pace;
        var visitorsRead = ReadAsync(pace.VisitorsLoadUrl, cancellationToken);
        var renderersRead = RenderersAsync(pace.PassLoadUrl, cancellationToken);
        return new SsrLoadSet(await visitorsRead, await renderersRead);
    }

    private async Task<IReadOnlyDictionary<string, SsrLoad>> RenderersAsync(
        string? passUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(passUrl))
        {
            return new Dictionary<string, SsrLoad>();
        }

        var url = new Uri(passUrl);
        var addresses = await renderers.CurrentAsync(cancellationToken);
        var urls = addresses.Count == 0
            ? [url]
            : addresses.Select(address => new UriBuilder(url) { Host = address.ToString() }.Uri)
                .ToList();
        var reads = urls.Select(async each => (each.Host, Load: await loads.ReadAsync(
            each,
            cancellationToken)));
        var answered = await Task.WhenAll(reads);
        return answered.Where(static read => read.Load is not null)
            .ToDictionary(
                static read => read.Host,
                static read => read.Load!,
                StringComparer.Ordinal);
    }

    private Task<SsrLoad?> ReadAsync(string? url, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(url)
            ? Task.FromResult<SsrLoad?>(null)
            : loads.ReadAsync(new Uri(url), cancellationToken);
}
