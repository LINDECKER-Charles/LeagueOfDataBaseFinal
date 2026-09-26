using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Derived.Champions;
using LoDb.Domain.Tests.Catalog.Samples;

namespace LoDb.Domain.Tests.Derived.Champions;

/// <summary>
/// The chromas Data Dragon lists as skins go once CommunityDragon attaches them to a parent.
/// </summary>
public sealed class ChromaSkinsTests
{
    [Fact]
    public void Up9ChromaSkinsAreDropped()
    {
        IReadOnlyList<Skin> skins =
        [
            ChampionSamples.Skin("799000", "default"),
            ChampionSamples.Skin("799001", "Battle Bunny", 799002, 799003),
            ChampionSamples.Skin("799002", "Battle Bunny (Ruby)"),
            ChampionSamples.Skin("799003", "Battle Bunny (Emerald)"),
        ];

        var kept = ChromaSkins.Without(skins);

        Assert.Equal(["799000", "799001"], kept.Select(skin => skin.Id));
    }

    [Fact]
    public void WithoutChromaDataTheSkinsStayWhole()
    {
        IReadOnlyList<Skin> skins =
        [
            ChampionSamples.Skin("799000", "default"),
            ChampionSamples.Skin("799002", "Battle Bunny (Ruby)"),
        ];

        Assert.Equal(skins, ChromaSkins.Without(skins));
    }
}
