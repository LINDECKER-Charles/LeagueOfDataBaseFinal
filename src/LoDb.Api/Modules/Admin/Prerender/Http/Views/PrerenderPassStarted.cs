namespace LoDb.Api.Modules.Admin.Prerender.Http.Views;

/// <summary>The pass just opened, running at once: the worker takes it at its next tick.</summary>
internal sealed record PrerenderPassStarted
{
    public required long Id { get; init; }

    public required int Epoch { get; init; }

    /// <summary>
    /// Its units, one per (version, locale) of its scope among the older patches: none when
    /// the scope matches no older patch.
    /// </summary>
    public required int Units { get; init; }
}
