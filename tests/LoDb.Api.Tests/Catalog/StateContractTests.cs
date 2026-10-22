using System.Net;
using System.Text.Json;

namespace LoDb.Api.Tests.Catalog;

/// <summary>
/// The answers before the index of states is built, as on a database migrated but not
/// indexed: every page is outside the index and takes the residual rule, and no version has a
/// page of changes.
/// </summary>
[Collection(CatalogApiGroup.Name)]
public sealed class StateContractTests(CatalogApiFixture api)
{
    [Fact]
    public async Task DetailOutsideTheIndexNamesItsCanonicalWithoutAState()
    {
        var boots = await api.GetJsonAsync("/api/catalog/16.19.1/en_US/items/1001");

        Assert.Equal("items/1001-boots", boots.GetProperty("canonical").Text("path"));
        Assert.Equal(JsonValueKind.Null, boots.GetProperty("state").ValueKind);
        Assert.Equal(0, boots.GetProperty("history").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, boots.GetProperty("next").ValueKind);
        Assert.Equal(JsonValueKind.Null, boots.GetProperty("changes").ValueKind);
        Assert.True(boots.GetProperty("presentInLatest").GetBoolean());
    }

    [Fact]
    public async Task NoVersionHasAPageOfChanges()
    {
        var problem = await api.GetProblemAsync(
            "/api/catalog/16.19.1/en_US/changes",
            HttpStatusCode.NotFound);
        var meta = await api.GetJsonAsync("/api/meta");

        Assert.Equal("no-patch-page", problem.Text("code"));
        Assert.True(meta.IsNull("oldestChangesVersion"));
    }
}
