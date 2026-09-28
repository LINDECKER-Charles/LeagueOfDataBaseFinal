using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Paths;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Egress;
using LoDb.Testing.Fixtures;

namespace LoDb.Ingestion.Tests.Ddragon;

/// <summary>
/// The two complete versions carry every image the ingestion fetches, so that the pipeline's
/// tests can ingest them whole without a network.
/// </summary>
public sealed class CompleteVersionImageTests
{
    public static TheoryData<string> CompleteVersions =>
        [DdragonFixtures.Latest.Value, DdragonFixtures.Previous.Value];

    [Theory]
    [MemberData(nameof(CompleteVersions))]
    public async Task EveryIngestedImageIsRecordedPresent(string version)
    {
        using var harness = ReplayHarness.Create();

        var paths = await IngestedImagesAsync(harness, PatchVersion.Parse(version));

        Assert.NotEmpty(paths);
        foreach (var path in paths)
        {
            var outcome = await UpstreamImageTests.FetchAsync(harness, path);
            Assert.True(outcome is FetchOutcome.Present, $"{path}: {outcome}");
        }
    }

    private static async Task<HashSet<string>> IngestedImagesAsync(
        ReplayHarness harness,
        PatchVersion version)
    {
        var scope = ReplayHarness.Scope(version.Value, "en_US");
        var chromas = await harness.Datasets.ReadChromasAsync(version, ReplayHarness.Token);
        var champions = await harness.Datasets.ReadChampionsAsync(
            scope, chromas, ReplayHarness.Token);
        var items = await harness.Datasets.ReadItemsAsync(scope, ReplayHarness.Token);
        var summoners = await harness.Datasets.ReadSummonersAsync(scope, ReplayHarness.Token);
        var runes = await harness.Datasets.ReadRunesAsync(scope, ReplayHarness.Token);
        return
        [
            .. champions.Entries.SelectMany(champion => ChampionImages(version, champion)),
            .. items.Entries.Select(item => DdragonImagePath.Item(version, item.Image)),
            .. summoners.Entries.Select(spell => DdragonImagePath.Spell(version, spell.Image)),
            .. runes.Entries.SelectMany(static tree => tree.Slots
                .SelectMany(static slot => slot.Runes)
                .Select(static rune => DdragonImagePath.RuneIcon(rune.Icon))
                .Prepend(DdragonImagePath.RuneIcon(tree.Icon))),
        ];
    }

    private static IEnumerable<string> ChampionImages(
        PatchVersion version,
        ChampionDetail champion) =>
        champion.Spells
            .Select(spell => DdragonImagePath.Spell(version, spell.Image))
            .Append(DdragonImagePath.Champion(version, champion.Summary.Image))
            .Concat(champion.Passive is { } passive
                ? [DdragonImagePath.Passive(version, passive.Image)]
                : []);
}
