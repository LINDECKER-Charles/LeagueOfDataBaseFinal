using LoDb.Domain.Derived.Ranges;
using LoDb.Domain.Tests.Catalog.Samples;

namespace LoDb.Domain.Tests.Derived.Ranges;

/// <summary>
/// Melee up to 325, ranged from 350: Riot's own classification, not the widest gap.
/// </summary>
public sealed class AttackRangeTests
{
    [Theory]
    [InlineData("Akali", 125, AttackRangeClass.Melee)]
    [InlineData("Nilah", 225, AttackRangeClass.Melee)]
    [InlineData("Rakan", 300, AttackRangeClass.Melee)]
    [InlineData("Lillia", 325, AttackRangeClass.Melee)]
    [InlineData("Urgot", 350, AttackRangeClass.Ranged)]
    [InlineData("Ahri", 550, AttackRangeClass.Ranged)]
    public void Up13MeleeUpTo325RangedFrom350(string id, double range, AttackRangeClass expected)
    {
        var champion = ChampionSamples.Named(id, id) with
        {
            Stats = new Dictionary<string, double> { [AttackRange.StatKey] = range },
        };

        Assert.Equal(expected, AttackRange.ClassOf(champion));
    }

    [Fact]
    public void AVersionWithoutRangeHasNoClass()
    {
        Assert.Null(AttackRange.ClassOf(ChampionSamples.Named("Ahri", "Ahri")));
    }
}
