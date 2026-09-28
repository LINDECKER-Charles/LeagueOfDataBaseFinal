using System.Net;
using System.Text.Json;

namespace LoDb.Api.Tests.Catalog.WarmUp;

/// <summary>
/// The loader's stream: a cold version's datasets, then the images of the named lists, each
/// frame the whole state; a warm version done at once; a refused catalog ending the stream
/// with the code a catalog call would answer.
/// </summary>
[Collection(CatalogApiGroup.Name)]
public sealed class CatalogWarmUpTests(CatalogApiFixture api)
{
    [Fact]
    public async Task ColdVersionFetchesTheImagesOfItsListsThenIsDone()
    {
        const string Cold = "/api/catalog/16.18.1/en_US";
        using var response =
            await api.GetAsync($"{Cold}/warm-up?resources=champions&resources=summoners");
        var frames = await FramesOfAsync(response);

        Assert.Equal("no", Assert.Single(response.Headers.GetValues("X-Accel-Buffering")));
        Assert.Equal("preparing", frames[0].Text("stage"));
        var done = frames[^1];
        Assert.Equal("done", done.Text("stage"));
        Assert.True(done.GetProperty("total").GetInt32() > 0);
        Assert.Equal(done.GetProperty("total").GetInt32(), done.GetProperty("settled").GetInt32());
        Assert.Equal(["champions", "summoners"], done.Pluck("resources", "resource"));
        Assert.All(done.Items("resources"), static resource =>
            Assert.True(resource.GetProperty("ready").GetBoolean()));
        Assert.False(string.IsNullOrEmpty(done.GetProperty("latest").Text("name")));
        using var list = await api.GetAsync($"{Cold}/summoners");
        Assert.Null(list.Headers.RetryAfter);
        Assert.DoesNotContain(
            "\"status\":\"pending\"",
            await list.Content.ReadAsStringAsync(CatalogApiFixture.Token),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task WarmVersionIsDoneWithoutFetching()
    {
        using var response =
            await api.GetAsync("/api/catalog/16.19.1/en_US/warm-up?resources=items&resources=runes");
        var done = (await FramesOfAsync(response))[^1];

        Assert.Equal(("done", 0, 0), (done.Text("stage"), done.GetProperty("total").GetInt32(),
            done.GetProperty("settled").GetInt32()));
        Assert.All(done.Items("resources"), static resource =>
            Assert.True(resource.GetProperty("ready").GetBoolean()));
        Assert.True(done.IsNull("latest"));
    }

    [Theory]
    [InlineData("99.1.1", "unknown-version")]
    [InlineData("latest", "invalid-version")]
    public async Task RefusedCatalogEndsTheStreamWithItsCode(string version, string code)
    {
        using var response = await api.GetAsync($"/api/catalog/{version}/en_US/warm-up");
        var last = (await FramesOfAsync(response))[^1];

        Assert.Equal(("failed", code), (last.Text("stage"), last.Text("failure")));
    }

    [Fact]
    public async Task UnknownResourceIsRefused()
    {
        var problem = await api.GetProblemAsync(
            "/api/catalog/16.19.1/en_US/warm-up?resources=skins",
            HttpStatusCode.BadRequest);

        Assert.Equal("invalid-resource", problem.Text("code"));
    }

    private static async Task<IReadOnlyList<JsonElement>> FramesOfAsync(
        HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        var frames = SseFrames.Parse(
            await response.Content.ReadAsStringAsync(CatalogApiFixture.Token));
        Assert.NotEmpty(frames);
        return frames;
    }
}
