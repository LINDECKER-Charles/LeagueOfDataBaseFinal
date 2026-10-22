using System.Net;
using LoDb.Domain.Catalog;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.States.Snapshots;
using LoDb.Testing.States;

namespace LoDb.Api.Tests.Legacy;

/// <summary>
/// Where the index of states lands an old detail URL (ADR 0005, amendment of 2026-10-22):
/// at the version it names, the opener of a closed state or the short URL of an open one;
/// without a version, the opener of the last state of an entry the latest patch lacks. The
/// index covers 7.22.1, 8.7.1, 16.18.1 and 16.19.1: 3001 is Abyssal Mask until 8.7.1 and
/// Evenshroud since 16.18.1, Molten Edge (3371) is retired at 16.18.1, Teemo is unchanged
/// since 8.7.1.
/// </summary>
[Collection(LegacyApiGroup.Name)]
public sealed class LegacyStateRedirectTests : IDisposable
{
    private static readonly IndexedEntity Mask = new(ResourceType.Items, "3001");
    private static readonly IndexedEntity MoltenEdge = new(ResourceType.Items, "3371");
    private static readonly IndexedEntity Teemo = new(ResourceType.Champions, "Teemo");

    private readonly LegacyApiFixture api;

    public LegacyStateRedirectTests(LegacyApiFixture api)
    {
        this.api = api;
        api.States.Snapshot = TestStates.Snapshot(
            ["7.22.1", "8.7.1", "16.18.1", "16.19.1"],
            [
                TestStates.Closed(Mask, "7.22.1", "8.7.1") with
                {
                    Path = "items/3001-abyssal-mask",
                },
                TestStates.Open(Mask, "16.18.1") with
                {
                    Path = "items/3001-evenshroud",
                    PreviousOpener = PatchVersion.Parse("7.22.1"),
                },
                TestStates.Closed(MoltenEdge, "7.22.1", "8.7.1") with
                {
                    Path = "items/3371-molten-edge",
                },
                TestStates.Open(Teemo, "8.7.1"),
            ]);
    }

    // The latest catalog turns the name into an id; the index lands on the state of 8.7.1.
    [Fact]
    public async Task ClosedStateLandsOnItsOpener() =>
        Assert.Equal(
            "/fr/7.22.1/items/3001-abyssal-mask",
            await api.LocationOfAsync("/8.7.1/object/3001?lang=fr_FR"));

    [Theory]
    [InlineData("/16.18.1/champion/Teemo", "/en/champions/Teemo")]
    [InlineData("/16.18.1/object/3001", "/en/items/3001-evenshroud")]
    public async Task OpenStateLandsOnTheShortUrl(string oldUrl, string newUrl) =>
        Assert.Equal(newUrl, await api.LocationOfAsync(oldUrl));

    // The index knows the entry the latest catalog lacks: 8.7.1, never stored here, stays
    // unread.
    [Fact]
    public async Task EntryGoneFromTheLatestPatchLandsOnItsOpenerUnread()
    {
        var target = await api.LocationOfAsync("/8.7.1/object/3371");

        Assert.Equal("/en/7.22.1/items/3371-molten-edge", target);
        Assert.False(Directory.Exists(Path.Combine(api.StorageRoot, "data", "8.7.1")));
    }

    [Fact]
    public async Task RetiredEntryWithoutAVersionLandsOnTheOpenerOfItsLastState() =>
        Assert.Equal(
            "/en/7.22.1/items/3371-molten-edge",
            await api.LocationOfAsync("/object/3371"));

    // 3.13.24 is older than the index: the short URL of an entry the latest patch holds, the
    // list of the version, never stored, for one it lacks.
    [Fact]
    public async Task VersionWithoutAStateKeepsTheFormerFallbacks()
    {
        var boots = await api.LocationOfAsync("/3.13.24/object/1001");
        using var gone = await api.SendAsync("/3.13.24/object/3371");

        Assert.Equal("/en/items/1001-boots", boots);
        Assert.Equal(HttpStatusCode.Found, gone.StatusCode);
        Assert.Equal("/en/3.13.24/items", gone.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task UnreadableIndexAnswersARetry()
    {
        api.States.IsUnavailable = true;

        using var response = await api.SendAsync("/8.7.1/object/3001");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter);
    }

    public void Dispose()
    {
        api.States.Snapshot = EntityStateSnapshot.Empty;
        api.States.IsUnavailable = false;
    }
}
