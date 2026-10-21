namespace LoDb.Api.Modules.Admin.Prerender.Http.Views;

/// <summary>The pages of one older patch in the served epoch.</summary>
internal sealed record PrerenderVersionVolume
{
    public required string Version { get; init; }

    /// <summary>Locales of the version with pages stored, out of the site's 21.</summary>
    public required int Locales { get; init; }

    public required long Pages { get; init; }

    public required long Bytes { get; init; }
}
