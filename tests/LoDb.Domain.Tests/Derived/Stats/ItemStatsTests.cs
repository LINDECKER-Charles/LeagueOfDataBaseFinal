using System.Text.Json;
using LoDb.Domain.Derived.Stats;

namespace LoDb.Domain.Tests.Derived.Stats;

/// <summary>
/// An item stat block reads the 12 classic Data Dragon keys, in display order, zeros dropped.
/// </summary>
public sealed class ItemStatsTests
{
    [Fact]
    public void Up13OnlyTheTwelveClassicKeysAreRead()
    {
        Assert.Equal(
        [
            "FlatPhysicalDamageMod", "FlatMagicDamageMod", "PercentAttackSpeedMod",
            "FlatCritChanceMod", "PercentLifeStealMod", "FlatHPPoolMod", "FlatHPRegenMod",
            "FlatArmorMod", "FlatSpellBlockMod", "FlatMPPoolMod", "FlatMovementSpeedMod",
            "PercentMovementSpeedMod",
        ],
        ItemStats.Keys.Select(key => key.DdragonKey));
    }

    [Fact]
    public void RowsFollowTheDisplayOrderNotTheDatasetOrder()
    {
        var rows = ItemStats.Of(new Dictionary<string, double>
        {
            ["FlatCritChanceMod"] = 0.25,
            ["PercentAttackSpeedMod"] = 0.25,
            ["FlatPhysicalDamageMod"] = 75,
        });

        Assert.Equal(
            [GameStat.AttackDamage, GameStat.AttackSpeed, GameStat.CritChance],
            rows.Select(row => row.Stat));
        Assert.Equal([false, true, true], rows.Select(row => row.IsPercent));
        Assert.Equal([75, 0.25, 0.25], rows.Select(row => row.Value));
    }

    [Fact]
    public void Up13ZerosAndUnknownKeysAreDropped()
    {
        var rows = ItemStats.Of(new Dictionary<string, double>
        {
            ["FlatArmorMod"] = 0,
            ["FlatLifestealMod"] = 10,
            ["FlatHPPoolMod"] = 300,
        });

        var row = Assert.Single(rows);
        Assert.Equal(new ItemStat { Stat = GameStat.Health, IsPercent = false, Value = 300 }, row);
    }

    [Fact]
    public void FlatAndPercentMoveSpeedAreTwoRows()
    {
        var rows = ItemStats.Of(new Dictionary<string, double>
        {
            ["PercentMovementSpeedMod"] = 0.05,
            ["FlatMovementSpeedMod"] = 25,
        });

        Assert.Equal(
        [
            new ItemStat { Stat = GameStat.MoveSpeed, IsPercent = false, Value = 25 },
            new ItemStat { Stat = GameStat.MoveSpeed, IsPercent = true, Value = 0.05 },
        ],
        rows);
    }

    [Fact]
    public void NoStatsMeansNoRows()
    {
        Assert.Empty(ItemStats.Of(null));
        Assert.Empty(ItemStats.Of(new Dictionary<string, double>()));
    }

    [Fact]
    public void TheSnakeCaseNamingPolicyWritesTheLegacyStatCodes()
    {
        var codes = Enum.GetValues<GameStat>()
            .Select(stat => JsonNamingPolicy.SnakeCaseLower.ConvertName(stat.ToString()));

        Assert.Equal(
        [
            "attack_damage", "ability_power", "attack_speed", "crit_chance", "life_steal",
            "health", "health_regen", "armor", "magic_resist", "mana", "mana_regen",
            "move_speed", "attack_range",
        ],
        codes);
    }
}
