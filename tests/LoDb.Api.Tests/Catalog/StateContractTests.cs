using System.Net;
using System.Text.Json;

namespace LoDb.Api.Tests.Catalog;

/// <summary>
/// The contract of the index of states before the index answers it: a detail always names
/// a canonical and carries its state's fields, and no version has a page of changes yet.
/// </summary>
[Collection(CatalogApiGroup.Name)]
public sealed class StateContractTests(CatalogApiFixture api)
{
    [Fact]
    public async Task DetailNamesItsCanonicalAndItsStateFields()
    {
        var boots = await api.GetJsonAsync("/api/catalog/16.19.1/en_US/items/1001");

        Assert.Equal("items/1001-boots", boots.GetProperty("canonical").Text("path"));
        Assert.Equal(JsonValueKind.Null, boots.GetProperty("state").ValueKind);
        Assert.Equal(0, boots.GetProperty("history").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, boots.GetProperty("next").ValueKind);
        Assert.Equal(JsonValueKind.Null, boots.GetProperty("changes").ValueKind);
        Assert.Equal(JsonValueKind.False, boots.GetProperty("presentInLatest").ValueKind);
    }

    [Fact]
    public async Task PatchHasNoPageOfChangesYet()
    {
        var problem = await api.GetProblemAsync(
            "/api/catalog/16.19.1/en_US/changes",
            HttpStatusCode.NotFound);

        Assert.Equal("no-patch-page", problem.Text("code"));
    }
}
