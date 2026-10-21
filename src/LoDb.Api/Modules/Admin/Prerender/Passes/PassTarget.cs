using LoDb.Infrastructure.Persistence.Prerender;

namespace LoDb.Api.Modules.Admin.Prerender.Passes;

/// <summary>Where a pass moves, and why when the worker pauses it itself.</summary>
/// <param name="State">The state it moves to.</param>
/// <param name="Reason">
/// Why the pass paused itself (the store or the SSR refusing it); null for any other move.
/// </param>
internal sealed record PassTarget(PrerenderPassState State, string? Reason = null);
