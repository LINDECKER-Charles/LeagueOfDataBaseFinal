using LoDb.Ingestion.Tests.Ddragon;
using LoDb.Testing.Fixtures;

namespace LoDb.Ingestion.Tests.Normalization;

/// <summary>
/// Rune paths in upstream order, keystone row first.
/// </summary>
public sealed class RuneDatasetTests
{
    [Fact]
    public async Task TreesKeepTheUpstreamOrderAndTheKeystoneRowFirst()
    {
        using var harness = ReplayHarness.Create();

        var runes = await harness.Datasets.ReadRunesAsync(
            ReplayHarness.Scope(DdragonFixtures.Latest.Value, "ko_KR"), ReplayHarness.Token);

        Assert.Equal("ko_KR", runes.ContentLanguage);
        Assert.Equal([8100, 8000], runes.Entries.Select(static tree => tree.Id));
        var precision = runes.Entries[1];
        Assert.Contains(precision.Slots[0].Runes, static rune => rune.Id == 8005);
        Assert.All(
            precision.Slots.SelectMany(static slot => slot.Runes),
            static rune => Assert.EndsWith(".png", rune.Icon, StringComparison.Ordinal));
    }
}
