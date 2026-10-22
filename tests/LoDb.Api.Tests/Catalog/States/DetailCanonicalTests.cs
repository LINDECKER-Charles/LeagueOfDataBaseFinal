using System.Net;
using System.Text.Json;
using LoDb.Domain.Catalog;
using LoDb.Domain.States.Fields;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.States.Snapshots;
using LoDb.Testing.States;

namespace LoDb.Api.Tests.Catalog.States;

/// <summary>
/// The canonical a detail names, one test per row of the table of the spec (§ 3.3), over an
/// index covering 8.7.1, 16.18.1 and 16.19.1, the latest version: Ahri unchanged since 8.7.1;
/// Teemo new at 16.18.1, changed at 16.19.1; Wukong unchanged from 8.7.1 to 16.18.1. The pages
/// read here are no opener but of a new entity, which compares nothing.
/// </summary>
/// <remarks>
/// The residual rule's page that is its own canonical needs an entity the latest version
/// lacks, which the recording only holds in versions without images:
/// <c>StateViewTests</c> proves it.
/// </remarks>
[Collection(StateApiGroup.Name)]
public sealed class DetailCanonicalTests : IDisposable
{
    private const string Archived = "8.7.1";
    private const string Previous = "16.18.1";
    private const string Latest = "16.19.1";

    private static readonly IndexedEntity Ahri = new(ResourceType.Champions, "Ahri");
    private static readonly IndexedEntity Teemo = new(ResourceType.Champions, "Teemo");
    private static readonly IndexedEntity Wukong = new(ResourceType.Champions, "MonkeyKing");
    private static readonly IndexedEntity Garen = new(ResourceType.Champions, "Garen");

    private readonly StateApiFixture api;

    public DetailCanonicalTests(StateApiFixture api)
    {
        this.api = api;
        api.Seed(
            [Archived, Previous, Latest],
            [
                TestStates.Open(Ahri, Archived),
                TestStates.Closed(Teemo, Previous, Previous),
                TestStates.Open(Teemo, Latest) with
                {
                    PreviousOpener = V(Previous),
                    ChangedFields = [FieldKey.Of(ChangeField.Title)],
                },
                TestStates.Closed(Wukong, Archived, Previous),
                TestStates.Open(Wukong, Latest) with { PreviousOpener = V(Archived) },
            ]);
    }

    [Fact]
    public async Task LatestPageIsItsOwnShortUrl()
    {
        var ahri = await api.GetJsonAsync("/api/catalog/16.19.1/en_US/champions/Ahri");

        Assert.Equal((null, "champions/Ahri"), Link(ahri.GetProperty("canonical")));
        Assert.True(ahri.GetProperty("isPresentInLatest").GetBoolean());
    }

    [Fact]
    public async Task OpenerOfAClosedStateIsItsOwnCanonical()
    {
        var teemo = await api.GetJsonAsync("/api/catalog/16.18.1/en_US/champions/Teemo");

        Assert.Equal((Previous, "champions/Teemo"), Link(teemo.GetProperty("canonical")));
        Assert.True(teemo.GetProperty("state").GetProperty("isOpener").GetBoolean());
    }

    [Fact]
    public async Task OtherVersionOfAClosedStateNamesItsOpenerInItsLanguage()
    {
        var wukong = await api.GetJsonAsync("/api/catalog/16.18.1/fr_FR/champions/MonkeyKing");

        Assert.Equal((Archived, "champions/MonkeyKing"), Link(wukong.GetProperty("canonical")));
    }

    [Fact]
    public async Task OpenStateOfAPastVersionNamesTheShortUrl()
    {
        var ahri = await api.GetJsonAsync("/api/catalog/16.18.1/en_US/champions/Ahri");

        Assert.Equal((null, "champions/Ahri"), Link(ahri.GetProperty("canonical")));
        Assert.True(ahri.GetProperty("state").GetProperty("isOpen").GetBoolean());
    }

    [Fact]
    public async Task VersionPastALaggingIndexStandsInTheOpenState()
    {
        api.Seed([Archived], [TestStates.Open(Ahri, Archived)]);

        var ahri = await api.GetJsonAsync("/api/catalog/16.18.1/en_US/champions/Ahri");

        Assert.Equal((null, "champions/Ahri"), Link(ahri.GetProperty("canonical")));
        Assert.Equal(Archived, ahri.GetProperty("state").Text("openerVersion"));
    }

    [Fact]
    public async Task VersionOlderThanTheIndexTakesTheResidualRule()
    {
        api.Seed([Latest], [TestStates.Open(Garen, Latest)]);

        var garen = await api.GetJsonAsync("/api/catalog/16.18.1/fr_FR/champions/garen");

        Assert.Equal((null, "champions/Garen"), Link(garen.GetProperty("canonical")));
        Assert.Equal(JsonValueKind.Null, garen.GetProperty("state").ValueKind);
        Assert.Equal(0, garen.GetProperty("history").GetArrayLength());
    }

    [Fact]
    public async Task UnreadableIndexAnswersARetry()
    {
        api.States.IsUnavailable = true;

        using var response = await api.GetAsync("/api/catalog/16.18.1/en_US/champions/Ahri");
        var problem = await api.GetProblemAsync(
            "/api/catalog/16.19.1/en_US/items/1001",
            HttpStatusCode.ServiceUnavailable);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter);
        Assert.Equal("state-index-unavailable", problem.Text("code"));
    }

    public void Dispose() => api.Reset();

    internal static (string? Version, string Path) Link(JsonElement link)
    {
        var version = link.GetProperty("version");
        return (
            version.ValueKind == JsonValueKind.Null ? null : version.GetString(),
            link.Text("path"));
    }

    private static PatchVersion V(string version) => PatchVersion.Parse(version);
}
