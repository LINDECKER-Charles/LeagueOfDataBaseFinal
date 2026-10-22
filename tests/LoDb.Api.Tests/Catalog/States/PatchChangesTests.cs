using System.Net;
using System.Text.Json;
using LoDb.Domain.Catalog;
using LoDb.Domain.States.Fields;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.States.Snapshots;
using LoDb.Testing.States;

namespace LoDb.Api.Tests.Catalog.States;

/// <summary>
/// <c>GET /api/catalog/{version}/{lang}/changes</c> over an index covering 8.7.1, 16.18.1 and
/// 16.19.1, the latest version: at 16.18.1, Boots of Speed becomes Boots, Garen appears, and
/// Molten Edge and Wukong are retired; at 16.19.1, Wukong comes back with another Q.
/// </summary>
[Collection(StateApiGroup.Name)]
public sealed class PatchChangesTests : IDisposable
{
    private const string Archived = "8.7.1";
    private const string Previous = "16.18.1";
    private const string Latest = "16.19.1";

    private static readonly IndexedEntity Boots = new(ResourceType.Items, "1001");
    private static readonly IndexedEntity MoltenEdge = new(ResourceType.Items, "3371");
    private static readonly IndexedEntity Garen = new(ResourceType.Champions, "Garen");
    private static readonly IndexedEntity Wukong = new(ResourceType.Champions, "MonkeyKing");
    private static readonly IndexedEntity Flash = new(ResourceType.Summoners, "SummonerFlash");

    private readonly StateApiFixture api;

    public PatchChangesTests(StateApiFixture api)
    {
        this.api = api;
        api.Seed(
            [Archived, Previous, Latest],
            [
                TestStates.Closed(Boots, Archived, Archived) with
                {
                    Path = "items/1001-boots-of-speed",
                },
                TestStates.Open(Boots, Previous) with
                {
                    Path = "items/1001-boots",
                    PreviousOpener = V(Archived),
                    ChangedFields = [FieldKey.Of(ChangeField.Name)],
                },
                TestStates.Closed(MoltenEdge, Archived, Archived) with
                {
                    Path = "items/3371-molten-edge",
                },
                TestStates.Open(Garen, Previous),
                TestStates.Closed(Wukong, Archived, Archived),
                TestStates.Open(Wukong, Latest) with
                {
                    PreviousOpener = V(Archived),
                    IsReturn = true,
                    ChangedFields = [FieldKey.Of(ChangeField.SpellName, "q")],
                },
                TestStates.Open(Flash, Archived),
            ]);
    }

    // Molten Edge is not in 16.18.1: its name comes from 8.7.1, its link is its last opener.
    [Fact]
    public async Task PastPatchListsWhatItChangedAndRetired()
    {
        var page = await api.GetJsonAsync("/api/catalog/16.18.1/en_US/changes");
        var groups = page.Items("groups");
        var items = groups[1].Items("entries");

        Assert.Equal((Previous, Archived), (page.Text("version"), page.Text("previousVersion")));
        Assert.True(page.GetProperty("hasChanges").GetBoolean());
        Assert.Equal(["champions", "items"], groups.Select(static group => group.Text("type")));
        Assert.Equal(
            [("Garen", "new"), ("Wukong", "retired")],
            groups[0].Items("entries").Select(Entry));
        Assert.Equal([("Boots", "changed"), ("Molten Edge", "retired")], items.Select(Entry));
        Assert.Equal("name", items[0].Items("changedFields").Single().Text("field"));
        Assert.Equal(
            (Previous, "items/1001-boots"),
            DetailCanonicalTests.Link(items[0].GetProperty("link")));
        Assert.Equal(
            (Archived, "items/3371-molten-edge"),
            DetailCanonicalTests.Link(items[1].GetProperty("link")));
        Assert.Equal(0, items[1].GetProperty("changedFields").GetArrayLength());
    }

    [Fact]
    public async Task LatestPatchLinksShortUrlsAndNamesSubjectsInItsLanguage()
    {
        var page = await api.GetJsonAsync("/api/catalog/16.19.1/fr_FR/changes");
        var wukong = page.Items("groups").Single().Items("entries").Single();
        var subject = wukong.Items("changedFields").Single().GetProperty("subject");

        Assert.Equal(("Wukong", "returned"), Entry(wukong));
        Assert.Equal(
            (null, "champions/MonkeyKing"),
            DetailCanonicalTests.Link(wukong.GetProperty("link")));
        Assert.Equal(("spellSlot", "q"), (subject.Text("kind"), subject.Text("key")));
        Assert.Equal("Écrasement", subject.Text("name"));
    }

    [Fact]
    public async Task PatchThatChangedNothingHasAnEmptyPage()
    {
        api.Seed([Previous, Latest], [TestStates.Open(Garen, Previous)]);

        var page = await api.GetJsonAsync("/api/catalog/16.19.1/en_US/changes");

        Assert.False(page.GetProperty("hasChanges").GetBoolean());
        Assert.Equal(0, page.GetProperty("groups").GetArrayLength());
    }

    // The oldest covered version has none to compare to; 7.22.1 is outside the index; an index
    // ahead of the reader covers 16.20.1, listed after the latest version.
    [Theory]
    [InlineData(Archived)]
    [InlineData("7.22.1")]
    [InlineData("16.20.1")]
    public async Task VersionWithoutAPageOfChangesIsNotFound(string version)
    {
        api.Seed([Archived, Previous, Latest, "16.20.1"], []);

        var problem = await api.GetProblemAsync(
            $"/api/catalog/{version}/en_US/changes",
            HttpStatusCode.NotFound);

        Assert.Equal("no-patch-page", problem.Text("code"));
    }

    [Fact]
    public async Task UnreadableIndexAnswersARetry()
    {
        api.States.IsUnavailable = true;

        using var response = await api.GetAsync("/api/catalog/16.18.1/en_US/changes");
        var problem = await api.GetProblemAsync(
            "/api/catalog/16.18.1/en_US/changes",
            HttpStatusCode.ServiceUnavailable);

        Assert.NotNull(response.Headers.RetryAfter);
        Assert.Equal("state-index-unavailable", problem.Text("code"));
    }

    public void Dispose() => api.Reset();

    private static (string Name, string Kind) Entry(JsonElement entry) =>
        (entry.Text("name"), entry.Text("kind"));

    private static PatchVersion V(string version) => PatchVersion.Parse(version);
}
