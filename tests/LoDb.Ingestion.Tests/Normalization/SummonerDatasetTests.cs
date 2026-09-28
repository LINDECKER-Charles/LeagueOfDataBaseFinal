using LoDb.Domain.Catalog.Summoners;
using LoDb.Domain.Editions;
using LoDb.Ingestion.Tests.Ddragon;
using LoDb.Testing.Fixtures;

namespace LoDb.Ingestion.Tests.Normalization;

/// <summary>
/// Summoner spells: the JADE mode makes a Classic spell, linked to its namesake (UP 6).
/// </summary>
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

    // KIWI_JADE is an ARAM variant, not LoL Classic.
    [Fact]
    public async Task OnlyTheJadeModeIsClassic()
    {
        var spells = await ReadAsync(DdragonFixtures.Latest.Value);

        var snowball = Single(spells, "SummonerSnowball");

        Assert.Contains("KIWI_JADE", snowball.Modes);
        Assert.Equal(Edition.Modern, snowball.Edition);
        Assert.Null(snowball.Counterpart);
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
