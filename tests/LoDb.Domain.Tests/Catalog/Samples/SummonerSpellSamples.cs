using LoDb.Domain.Catalog.Summoners;

namespace LoDb.Domain.Tests.Catalog.Samples;

/// <summary>
/// Summoner spells reduced to what a test states; everything else is neutral.
/// </summary>
internal static class SummonerSpellSamples
{
    internal static SummonerSpell Named(string id, string name, params string[] modes) => new()
    {
        Id = id,
        Key = "0",
        Name = name,
        Description = string.Empty,
        Image = id + ".png",
        Cooldown = [],
        Cost = [],
        Range = [],
        Modes = modes,
    };
}
