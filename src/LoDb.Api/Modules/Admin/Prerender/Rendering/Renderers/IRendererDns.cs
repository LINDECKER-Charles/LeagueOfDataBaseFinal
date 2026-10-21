using System.Net;

namespace LoDb.Api.Modules.Admin.Prerender.Rendering.Renderers;

/// <summary>Resolves the renderers' name (<see cref="RendererDns"/>; a fake in tests).</summary>
internal interface IRendererDns
{
    /// <summary>
    /// Every address of <paramref name="host"/>; empty when it cannot be resolved.
    /// </summary>
    Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken);
}
