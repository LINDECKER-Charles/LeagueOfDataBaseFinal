using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Tests.Catalog.Samples;

namespace LoDb.Domain.Tests.Catalog;

/// <summary>
/// Data Dragon writes -1 for a spell without charges: only a positive count is a charge.
/// </summary>
public sealed class SpellChargesTests
{
    [Theory]
    [InlineData(3, 3)]
    [InlineData(-1, null)]
    [InlineData(0, null)]
    [InlineData(null, null)]
    public void OnlyAPositiveAmmoCountIsACharge(int? maxAmmo, int? expected)
    {
        var ability = new ChampionSpell
        {
            Id = "AkaliE",
            Name = "Shuriken Flip",
            Description = string.Empty,
            Image = "AkaliE.png",
            MaxAmmo = maxAmmo,
        };
        var spell = SummonerSpellSamples.Named("SummonerFlash", "Flash") with { MaxAmmo = maxAmmo };

        Assert.Equal(expected, ability.Charges);
        Assert.Equal(expected, spell.Charges);
    }
}
