using LoDb.Domain.Catalog.Runes;
using LoDb.Domain.Paths;
using LoDb.Ingestion.Ddragon;
using LoDb.Ingestion.Egress;

namespace LoDb.Ingestion.Tests.Ddragon;

/// <summary>
/// The image answers the ingestion and the hotlinks rely on, as recorded.
/// </summary>
public sealed class UpstreamImageTests
{
    private const int Forbidden = 403;

    // A recorded skin of the reworked Fiddlesticks.
    private const int FiddlesticksSkin = 27;

    // 7.22.1 to 8.7.1 point their rune icons at .dds files the CDN refuses (UP 5): the path
    // is kept verbatim and its absence recorded, never rewritten to a guess.
    [Theory]
    [InlineData("7.22.1")]
    [InlineData("8.7.1")]
    public async Task DdsRuneIconsAreAbsent(string version)
    {
        using var harness = ReplayHarness.Create();
        var runes = await harness.Datasets.ReadRunesAsync(
            ReplayHarness.Scope(version, "en_US"), ReplayHarness.Token);

        var icons = Icons(runes.Entries).ToList();

        Assert.NotEmpty(icons);
        Assert.All(icons, static icon => Assert.EndsWith(".dds", icon, StringComparison.Ordinal));
        foreach (var icon in icons)
        {
            Assert.Equal(
                new FetchOutcome.Absent(Forbidden),
                await FetchAsync(harness, DdragonImagePath.RuneIcon(icon)));
        }
    }

    // The CDN keeps the reworked Fiddlesticks under Riot's internal spelling (UP 7). These
    // tests document the recorded upstream answers; the spelling rule is ChampionArt's.
    [Fact]
    public async Task FiddlesticksArtAnswersUnderItsInternalSpelling()
    {
        using var harness = ReplayHarness.Create();

        var path = DdragonImagePath.ChampionArt(
            "Fiddlesticks",
            ChampionArtKind.Centered,
            FiddlesticksSkin);

        var outcome = await FetchAsync(harness, path);

        Assert.IsType<FetchOutcome.Present>(outcome);
    }

    [Fact]
    public async Task FiddlesticksArtUnderItsPublicIdIsForbidden()
    {
        using var harness = ReplayHarness.Create();

        var outcome = await FetchAsync(
            harness,
            $"img/champion/centered/Fiddlesticks_{FiddlesticksSkin}.jpg");

        Assert.Equal(new FetchOutcome.Absent(Forbidden), outcome);
    }

    private static IEnumerable<string> Icons(IEnumerable<RuneTree> trees) =>
        trees.SelectMany(static tree => tree.Slots
            .SelectMany(static slot => slot.Runes)
            .Select(static rune => rune.Icon)
            .Prepend(tree.Icon));

    internal static async Task<FetchOutcome> FetchAsync(ReplayHarness harness, string path)
    {
        var outcome = await harness.Fetcher.FetchAsync(
            DdragonUrls.Image(path), ReplayHarness.Token);
        if (outcome is FetchOutcome.Present present)
        {
            await present.Content.DisposeAsync();
        }

        return outcome;
    }
}
