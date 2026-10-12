using LoDb.Domain.Catalog.Summoners;
using LoDb.Domain.Editions;
using LoDb.Ingestion.Tests.Ddragon;
using LoDb.Testing.Fixtures;

namespace LoDb.Ingestion.Tests.Normalization;

/// <summary>
/// Summoner spells: the JADE mode makes a Classic spell, linked to its namesake (UP 6).
/// </summary>
/// <remarks>
/// Which modes make a Classic spell is SummonerSpellEdition's, tested in the Domain: the
/// first test only proves the mapper applies it and links the twins.
/// </remarks>
public sealed class SummonerDatasetTests
{
    [Fact]
    public async Task JadeSpellsAreClassicTwins()
    {
        var spells = await ReadAsync(DdragonFixtures.Latest.Value);

        var flash = Single(spells, "SummonerFlash");
        var jade = Single(spells, "SummonerFlash_Jade");

        Assert.Equal(Edition.Modern, flash.Edition);
        Assert.Equal(Edition.Classic, jade.Edition);
        Assert.Equal("SummonerFlash_Jade", flash.Counterpart?.Id);
        Assert.Equal("SummonerFlash", jade.Counterpart?.Id);
    }

    // maxammo is written as a string ("-1"): read as a number, it means no charges.
    [Fact]
    public async Task StringAmmoReadsAsANumber()
    {
        var flash = Single(await ReadAsync(DdragonFixtures.Latest.Value), "SummonerFlash");

        Assert.Equal(-1, flash.MaxAmmo);
        Assert.Null(flash.Charges);
        Assert.NotEmpty(flash.Cooldown);
    }

    // 0.151.2 ships neither modes nor maxammo.
    [Fact]
    public async Task ZeroVersionSpellsHaveNoModes()
    {
        var spells = await ReadAsync("0.151.2");

        Assert.NotEmpty(spells);
        Assert.All(spells, static spell => Assert.Empty(spell.Modes));
        Assert.All(spells, static spell => Assert.Equal(Edition.Modern, spell.Edition));
    }

    private static SummonerSpell Single(IEnumerable<SummonerSpell> spells, string id) =>
        Assert.Single(spells, spell => spell.Id == id);

    private static async Task<IReadOnlyList<SummonerSpell>> ReadAsync(string version)
    {
        using var harness = ReplayHarness.Create();
        var spells = await harness.Datasets.ReadSummonersAsync(
            ReplayHarness.Scope(version, "en_US"), ReplayHarness.Token);
        return spells.Entries;
    }
}
