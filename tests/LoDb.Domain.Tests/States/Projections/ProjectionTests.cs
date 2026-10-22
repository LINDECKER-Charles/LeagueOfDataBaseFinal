using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.States.Projections;
using LoDb.Domain.Tests.Catalog.Samples;

namespace LoDb.Domain.Tests.States.Projections;

/// <summary>
/// What each projection holds: the fields the page shows, in the order of the templates,
/// their values as invariant strings, a field without a value left out.
/// </summary>
public sealed class ProjectionTests
{
    [Fact]
    public void AChampionShowsItsLinesSpellsBySlotAndSkinsById()
    {
        var summary = ChampionSamples.Named("Ahri", "Ahri") with
        {
            Blurb = "A <i>vastaya</i> fox.",
            Tags = ["Mage", "Assassin"],
            Partype = "Mana",
            Info = new ChampionRatings { Attack = 3, Defense = 4, Magic = 8, Difficulty = 5 },
            Stats = new Dictionary<string, double>
            {
                ["crit"] = 0,
                ["hpperlevel"] = 104,
                ["hp"] = 590,
                ["attackrange"] = 550,
            },
        };
        var champion = ChampionSamples.Detail(
            summary,
            [ChampionSamples.Spell("AhriQ", "7"), ChampionSamples.Spell("AhriW", "9")],
            [ChampionSamples.Skin("103000", "default", 103099)]);

        var fields = Fields(ChampionProjection.Of(champion));

        Assert.Equal(
            [
                "name=Ahri", "lore=A vastaya fox.", "roles=Mage|Assassin", "resource=Mana",
                "rating.attack=3", "rating.defense=4", "rating.magic=8", "rating.difficulty=5",
                "stat.health=590", "stat.attack_range=550", "statPerLevel.health=104",
                "spell.q.name=AhriQ", "spell.w.name=AhriW",
                "spell.q.cooldown=7", "spell.w.cooldown=9",
                "skins=103000", "skin.103000.name=default",
            ],
            fields);
    }

    [Fact]
    public void AnItemShowsItsClaimableMapsAndTagsAsSortedSets()
    {
        var item = ItemSamples.Named("3006", "Berserker's Greaves") with
        {
            Gold = ItemSamples.Gold(total: 1100, combine: 500),
            From = ["1001", "1042"],
            Stats = new Dictionary<string, double> { ["PercentAttackSpeedMod"] = 0.25 },
            Maps = ItemSamples.Maps((453, true), (30, true), (11, true), (12, false)),
            Tags = ["Boots", "AttackSpeed", "Boots"],
            Depth = 2,
        };

        Assert.Equal(
            [
                "name=Berserker's Greaves", "gold.total=1100", "gold.base=500", "gold.sell=0",
                "gold.purchasable=true", "recipe=1001|1042", "statPercent.attack_speed=0.25",
                "maps=11|30", "tags=AttackSpeed|Boots", "depth=2", "consumable=false",
            ],
            Fields(ItemProjection.Of(item)));
    }

    [Fact]
    public void ASummonerSpellShowsAGlobalRangeAndChargesOnlyWhenItStoresSome()
    {
        var teleport = SummonerSpellSamples.Named("SummonerTeleport", "Teleport", "URF", "ARAM")
            with
            {
                Cooldown = [360],
                Range = [25000],
                MaxAmmo = -1,
            };

        Assert.Equal(
            ["name=Teleport", "cooldown=360", "range=global", "modes=ARAM|URF"],
            Fields(SummonerProjection.Of(teleport)));
    }

    [Fact]
    public void ARunePathShowsItsRowsThenEachRune()
    {
        var tree = RuneTreeSamples.Named(
            8000,
            "Precision",
            [RuneTreeSamples.Rune(8005, "Press the Attack", "Hit <b>3</b> times.")],
            [RuneTreeSamples.Rune(9101, "Overheal"), RuneTreeSamples.Rune(9111, "Triumph")]);

        Assert.Equal(
            [
                "name=Precision", "slot.keystone=8005", "slot.row1=9101|9111",
                "rune.8005.name=Press the Attack", "rune.9101.name=Overheal",
                "rune.9111.name=Triumph", "rune.8005.longDesc=Hit 3 times.",
            ],
            Fields(RuneTreeProjection.Of(tree)));
    }

    private static List<string> Fields(IReadOnlyList<ProjectedField> projection) =>
        [.. projection.Select(static field =>
            $"{field.Key.ToDotted()}={string.Join('|', field.Values)}")];
}
