using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Catalog.Items;
using LoDb.Domain.States.Fingerprints;
using LoDb.Domain.States.Projections;
using LoDb.Domain.Tests.Catalog.Samples;

namespace LoDb.Domain.Tests.States.Fingerprints;

/// <summary>
/// The fingerprint covers what the page shows of the entity and nothing else: an image, a
/// chroma, an art number, a dictionary order or markup alone keep the state; a stat, a text or
/// a skin open a new one.
/// </summary>
public sealed class EntityFingerprintTests
{
    private static readonly Dictionary<string, double> LongSwordStats =
        new() { ["FlatPhysicalDamageMod"] = 10, ["FlatHPPoolMod"] = 0 };

    [Fact]
    public void FingerprintIsLowercaseHexSha256()
    {
        var fingerprint = EntityFingerprint.Of(ItemProjection.Of(LongSword()));

        Assert.Matches("^[0-9a-f]{64}$", fingerprint);
    }

    [Fact]
    public void AnImageAloneKeepsTheState()
    {
        var before = LongSword();

        StateAssert.Same(
            ItemProjection.Of(before),
            ItemProjection.Of(before with { Image = "1036-new.png" }));
    }

    [Fact]
    public void ADictionaryOrderOrAStatThePageHidesKeepsTheState()
    {
        var reordered = LongSwordStats.Reverse().ToDictionary();
        reordered["FlatEXPBonus"] = 3;
        reordered["FlatArmorMod"] = 0;

        StateAssert.Same(
            ItemProjection.Of(LongSword()),
            ItemProjection.Of(LongSword() with { Stats = reordered }));
    }

    [Fact]
    public void AStatOpensAState()
    {
        var stronger = new Dictionary<string, double>(LongSwordStats)
        {
            ["FlatPhysicalDamageMod"] = 12,
        };

        StateAssert.Changed(
            ItemProjection.Of(LongSword()),
            ItemProjection.Of(LongSword() with { Stats = stronger }),
            "stat.attack_damage");
    }

    [Fact]
    public void ATextOpensAStateWhileMarkupAloneDoesNot()
    {
        var before = LongSword() with { Description = "<stats>+10 AD</stats><br>Sharp." };
        var restyled = before with
        {
            Description = "<mainText><stats><attention>+10</attention> AD</stats><br><br>"
                + "Sharp.</mainText>",
        };

        StateAssert.Same(ItemProjection.Of(before), ItemProjection.Of(restyled));
        StateAssert.Changed(
            ItemProjection.Of(before),
            ItemProjection.Of(before with { Description = "<stats>+10 AD</stats><br>Sharper." }),
            "description");
    }

    [Fact]
    public void TheFieldOrderOfAProjectionDoesNotMatter()
    {
        var projection = ItemProjection.Of(LongSword());

        StateAssert.Same(projection, [.. projection.Reverse()]);
    }

    [Fact]
    public void AChromaOrAnArtNumberAloneKeepsTheState()
    {
        var before = Ahri(ChampionSamples.Skin("103001", "Dynasty Ahri", 103002));
        var after = Ahri(ChampionSamples.Skin("103001", "Dynasty Ahri", 103002, 103096) with
        {
            Number = 1,
        });

        StateAssert.Same(ChampionProjection.Of(before), ChampionProjection.Of(after));
    }

    [Fact]
    public void ASkinAddedOrRenamedOpensAState()
    {
        var dynasty = ChampionSamples.Skin("103001", "Dynasty Ahri");
        var before = ChampionProjection.Of(Ahri(dynasty));

        StateAssert.Changed(
            before,
            ChampionProjection.Of(Ahri(dynasty, ChampionSamples.Skin("103002", "Midnight Ahri"))),
            "skins",
            "skin.103002.name");
        StateAssert.Changed(
            before,
            ChampionProjection.Of(Ahri(dynasty with { Name = "Dynasty Ahri (Legacy)" })),
            "skin.103001.name");
    }

    private static Item LongSword() =>
        ItemSamples.Named("1036", "Long Sword") with { Stats = LongSwordStats };

    private static ChampionDetail Ahri(params Skin[] skins) =>
        ChampionSamples.Detail(ChampionSamples.Named("Ahri", "Ahri"), [], skins);
}
