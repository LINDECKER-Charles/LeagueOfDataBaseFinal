namespace LoDb.Api.Modules.Admin.Prerender.Http.Views;

/// <summary>The pass just opened, running at once: the worker takes it at its next tick.</summary>
internal sealed record PrerenderPassStarted
{
    public required long Id { get; init; }

    public required int Epoch { get; init; }

    /// <summary>
    /// Its units, one per (version, locale) of its scope among the older patches.
    /// </summary>
    public required int Units { get; init; }

    /// <summary>Units left out, already held in the epoch (<c>skipComplete</c>).</summary>
    public required int SkippedUnits { get; init; }

    /// <summary>Versions asked for that are not older patches, left out.</summary>
    public required IReadOnlyList<string> IgnoredVersions { get; init; }
}
