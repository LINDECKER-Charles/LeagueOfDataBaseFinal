namespace LoDb.Api.Modules.Admin.Prerender.Http.Views.Progress;

/// <summary>A (version, locale) of a pass.</summary>
internal sealed record PrerenderUnitKey
{
    public required string Version { get; init; }

    public required string Locale { get; init; }
}
