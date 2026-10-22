using System.Net;

namespace LoDb.Api.Tests.Catalog;

/// <summary>
/// The answers over the index of states the promotion of 16.19.1 built, the one version the
/// database holds: each entity's state opens there, at the start of the coverage, compared to
/// nothing, and no version has a page of changes.
/// </summary>
[Collection(CatalogApiGroup.Name)]
public sealed class StateContractTests(CatalogApiFixture api)
{
    [Fact]
    public async Task PromotionIndexesTheLatestVersionAsTheStartOfTheCoverage()
    {
        var boots = await api.GetJsonAsync("/api/catalog/16.19.1/en_US/items/1001");
        var state = boots.GetProperty("state");

        Assert.Equal("items/1001-boots", boots.GetProperty("canonical").Text("path"));
        Assert.True(boots.GetProperty("canonical").IsNull("version"));
        Assert.Equal(("16.19.1", "baseline"), (state.Text("openerVersion"), state.Text("kind")));
        Assert.True(state.GetProperty("isOpener").GetBoolean());
        Assert.True(state.IsNull("lastVersion"));
        Assert.Equal(["baseline"], boots.Pluck("history", "kind"));
        Assert.True(boots.IsNull("next"));
        Assert.True(boots.IsNull("changes"));
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
