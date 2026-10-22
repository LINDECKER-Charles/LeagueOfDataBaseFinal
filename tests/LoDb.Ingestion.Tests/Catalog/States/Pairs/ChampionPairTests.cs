using LoDb.Domain.States.Fingerprints;

namespace LoDb.Ingestion.Tests.Catalog.States.Pairs;

/// <summary>
/// Fiddlesticks in the recording: spells that change by slot, and an id whose case Data
/// Dragon changed between 3.13.24 and 7.21.1.
/// </summary>
public sealed class ChampionPairTests
{
    [Fact]
    public async Task OnlyTheSpellsThatChangedAreMarked()
    {
        var before = await CatalogPairs.ChampionAsync("7.22.1", "Fiddlesticks");
        var after = await CatalogPairs.ChampionAsync("8.7.1", "Fiddlesticks");

        var spellKeys = CatalogPairs.ChangedKeys(before, after)
            .Where(static key => key.StartsWith("spell.", StringComparison.Ordinal))
            .ToList();

        Assert.Contains("spell.e.cooldown", spellKeys);
        Assert.Contains("spell.r.cooldown", spellKeys);
        Assert.DoesNotContain(spellKeys, static key =>
            key.StartsWith("spell.q.", StringComparison.Ordinal)
            || key.StartsWith("spell.w.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnIdWhoseCaseChangedIsOneEntityThatChanges()
    {
        var old = await CatalogPairs.EnglishAsync("3.13.24");
        var renamed = await CatalogPairs.EnglishAsync("7.21.1");
        Assert.NotNull(old.Champions.Find("FiddleSticks"));
        Assert.Null(renamed.Champions.Find("FiddleSticks"));

        // Read by the folded key of the other version's id: the same entity on both sides.
        var before = await CatalogPairs.ChampionAsync("3.13.24", "Fiddlesticks");
        var after = await CatalogPairs.ChampionAsync("7.21.1", "FiddleSticks");
        var keys = CatalogPairs.ChangedKeys(before, after);

        Assert.NotEqual(EntityFingerprint.Of(before), EntityFingerprint.Of(after));
        Assert.DoesNotContain("name", keys);
        Assert.Contains("roles", keys);
    }
}
