using System.Net;
using System.Text.Json;
using LoDb.Domain.Catalog;
using LoDb.Domain.States.Fields;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.States.Snapshots;
using LoDb.Testing.States;

namespace LoDb.Api.Tests.Catalog.States;

/// <summary>
/// What a detail tells of its state, over an index covering 8.7.1, 16.18.1 and 16.19.1, the
/// latest version: Teemo new at 16.18.1, changed at 16.19.1; Garen whose Q changed at 16.18.1;
/// Boots of Speed renamed Boots at 16.18.1; Faerie Charm changed at 16.19.1.
/// </summary>
[Collection(StateApiGroup.Name)]
public sealed class DetailStateTests : IDisposable
{
    private const string NeedsDiffEngine =
        "Compares two projections: needs the projections and the diff engine of lot L1.";

    private const string Archived = "8.7.1";
    private const string Previous = "16.18.1";
    private const string Latest = "16.19.1";

    private static readonly IndexedEntity Teemo = new(ResourceType.Champions, "Teemo");
    private static readonly IndexedEntity Garen = new(ResourceType.Champions, "Garen");
    private static readonly IndexedEntity Boots = new(ResourceType.Items, "1001");
    private static readonly IndexedEntity Charm = new(ResourceType.Items, "1004");

    private readonly StateApiFixture api;

    public DetailStateTests(StateApiFixture api)
    {
        this.api = api;
        api.Seed(
            [Archived, Previous, Latest],
            [
                TestStates.Closed(Teemo, Previous, Previous),
                TestStates.Open(Teemo, Latest) with
                {
                    PreviousOpener = V(Previous),
                    ChangedFields = [FieldKey.Of(ChangeField.Title)],
                },
                TestStates.Closed(Garen, Archived, Archived),
                TestStates.Open(Garen, Previous) with
                {
                    PreviousOpener = V(Archived),
                    ChangedFields = [FieldKey.Of(ChangeField.SpellCooldown, "q")],
                },
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
                TestStates.Closed(Charm, Archived, Previous),
                TestStates.Open(Charm, Latest) with
                {
                    PreviousOpener = V(Archived),
                    ChangedFields = [FieldKey.Of(ChangeField.Plaintext)],
                },
            ]);
    }

    [Fact]
    public async Task FrozenPageTellsItsStateWhatFollowedAndItsHistory()
    {
        var teemo = await api.GetJsonAsync("/api/catalog/16.18.1/en_US/champions/Teemo");
        var state = teemo.GetProperty("state");
        var next = teemo.GetProperty("next");

        Assert.Equal((Previous, Previous), Range(state));
        Assert.False(state.GetProperty("isOpen").GetBoolean());
        Assert.Equal("new", state.Text("kind"));
        Assert.Equal(("changed", Latest), (next.Text("kind"), next.Text("version")));
        Assert.Equal(
            (Latest, "champions/Teemo"),
            DetailCanonicalTests.Link(next.GetProperty("link")));
        Assert.Equal([Previous], teemo.Pluck("history", "openerVersion"));
        Assert.True(teemo.GetProperty("presentInLatest").GetBoolean());
    }

    // The opener of a new entity compares nothing: its block reads no other catalog.
    [Fact]
    public async Task OpenerOfANewEntityShowsItAsNew()
    {
        var changes = (await api.GetJsonAsync("/api/catalog/16.18.1/en_US/champions/Teemo"))
            .GetProperty("changes");

        Assert.Equal("new", changes.Text("kind"));
        Assert.True(changes.IsNull("comparedVersion"));
        Assert.Equal(0, changes.GetProperty("fields").GetArrayLength());
    }

    // Unchanged since 16.18.1: the state names its fields in the page's language, links its
    // opener's versioned URL, and lists the history on the short URL, its open range unnamed.
    [Fact]
    public async Task ShortUrlTellsItsOpenStateAndTheWholeHistory()
    {
        var garen = await api.GetJsonAsync("/api/catalog/16.19.1/fr_FR/champions/Garen");
        var state = garen.GetProperty("state");
        var field = state.Items("changedFields").Single();
        var history = garen.Items("history");

        Assert.Equal((Previous, null), Range(state));
        Assert.False(state.GetProperty("isOpener").GetBoolean());
        Assert.Equal(
            (Previous, "champions/Garen"),
            DetailCanonicalTests.Link(state.GetProperty("openerLink")));
        Assert.Equal("spellCooldown", field.Text("field"));
        Assert.Equal("Coup décisif", field.GetProperty("subject").Text("name"));
        Assert.Equal([Previous, Archived], history.Select(entry => entry.Text("openerVersion")));
        Assert.True(history[0].IsNull("lastVersion"));
        Assert.True(history[0].Items("changedFields")[0].GetProperty("subject").IsNull("name"));
        Assert.True(garen.IsNull("next"));
        Assert.True(garen.IsNull("changes"));
    }

    // Boots of Speed's page at 16.18.1 opens a changed state: no answer without its block.
    [Fact]
    public async Task PinnedOpenerWaitsForTheCatalogItIsComparedTo()
    {
        api.Reader.HoldBack(V(Archived));

        using var response = await api.GetAsync("/api/catalog/16.18.1/en_US/items/1001");
        var problem = await api.GetProblemAsync(
            "/api/catalog/16.18.1/en_US/items/1001",
            HttpStatusCode.ServiceUnavailable);

        Assert.NotNull(response.Headers.RetryAfter);
        Assert.Equal("catalog-pending", problem.Text("code"));
    }

    [Fact]
    public async Task ShortUrlGoesWithoutItsBlockWhileTheCatalogComparedIsPending()
    {
        api.Reader.HoldBack(V(Previous));

        var charm = await api.GetJsonAsync("/api/catalog/16.19.1/en_US/items/1004");

        Assert.True(charm.GetProperty("state").GetProperty("isOpener").GetBoolean());
        Assert.True(charm.IsNull("changes"));
    }

    [Fact(Skip = NeedsDiffEngine)]
    public async Task OpenerShowsTheFieldsItsStateChanged()
    {
        var boots = await api.GetJsonAsync("/api/catalog/16.18.1/en_US/items/1001");
        var changes = boots.GetProperty("changes");
        var name = changes.Items("fields").Single();

        Assert.Equal(
            ("changed", Archived),
            (changes.Text("kind"), changes.Text("comparedVersion")));
        Assert.Equal(("name", "value"), (name.Text("field"), name.Text("kind")));
        Assert.Equal(["Boots of Speed"], name.Pluck("before", "value"));
        Assert.Equal(["Boots"], name.Pluck("after", "value"));
    }

    [Fact(Skip = NeedsDiffEngine)]
    public async Task ShortUrlOfAStateOpenedAtTheLatestShowsItsChanges()
    {
        var charm = await api.GetJsonAsync("/api/catalog/16.19.1/en_US/items/1004");
        var changes = charm.GetProperty("changes");
        var plaintext = changes.Items("fields").Single();

        Assert.Equal(Previous, changes.Text("comparedVersion"));
        Assert.Equal(("plaintext", "text"), (plaintext.Text("field"), plaintext.Text("kind")));
        Assert.Equal(0, plaintext.GetProperty("before").GetArrayLength());
        Assert.NotEqual(0, plaintext.GetProperty("segments").GetArrayLength());
    }

    public void Dispose() => api.Reset();

    private static (string Opener, string? Last) Range(JsonElement state) =>
        (
            state.Text("openerVersion"),
            state.IsNull("lastVersion") ? null : state.Text("lastVersion"));

    private static PatchVersion V(string version) => PatchVersion.Parse(version);
}
