using LoDb.Testing.Fixtures;

namespace LoDb.Ingestion.Tests.Ddragon;

/// <summary>
/// The recording stays small, whole and the only source of answers.
/// </summary>
public sealed class FixtureReplayTests
{
    private const long SizeBudgetBytes = 10_000_000;

    [Fact]
    public async Task UnrecordedUrlFailsTheTest()
    {
        using var harness = ReplayHarness.Create();
        var url = new Uri("https://ddragon.leagueoflegends.com/cdn/1.0.0/data/en_US/item.json");

        var failure = await Assert.ThrowsAsync<UnrecordedFixtureException>(
            () => harness.Fetcher.FetchAsync(url, ReplayHarness.Token));

        Assert.Equal(url, failure.Url);
        Assert.Equal([url], harness.Replay.Requests);
    }

    [Fact]
    public void RecordingFitsTheBudget()
    {
        var files = new DirectoryInfo(DdragonFixtures.Directory)
            .EnumerateFiles("*", SearchOption.AllDirectories);

        Assert.InRange(files.Sum(static file => file.Length), 1, SizeBudgetBytes);
    }

    [Fact]
    public void EveryRecordedBodyIsOnDisk()
    {
        var withBody = DdragonFixtures.Index.Responses
            .Where(static response => response is { Status: 200, Bodyless: false })
            .ToList();

        Assert.NotEmpty(withBody);
        Assert.All(withBody, static response => Assert.True(
            File.Exists(DdragonFixtures.BodyPath(new Uri(response.Url))), response.Url));
    }

    [Fact]
    public void RolesAreTheTwoNewestVersions()
    {
        Assert.Equal(DdragonFixtures.Versions[0], DdragonFixtures.Latest);
        Assert.Equal(DdragonFixtures.Versions[1], DdragonFixtures.Previous);
    }
}
