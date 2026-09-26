using LoDb.Domain.Catalog.Champions;

namespace LoDb.Domain.Tests.Catalog.Samples;

/// <summary>
/// Champions and skins reduced to what a test states; everything else is neutral.
/// </summary>
internal static class ChampionSamples
{
    /// <summary>A champion without resource, ratings nor stats.</summary>
    internal static ChampionSummary Named(string id, string name) => new()
    {
        Id = id,
        Key = "0",
        Name = name,
        Title = string.Empty,
        Blurb = string.Empty,
        Image = id + ".png",
        Tags = [],
        Stats = new Dictionary<string, double>(),
    };

    internal static Skin Skin(string id, string name, params int[] chromaIds) => new()
    {
        Id = id,
        Number = 0,
        Name = name,
        Chromas = [.. chromaIds.Select(chromaId => Chroma(chromaId, name))],
    };

    internal static Chroma Chroma(int id, string name, params string[] colors) => new()
    {
        Id = id,
        Name = name,
        Colors = colors,
        Image = "chroma.png",
    };
}
