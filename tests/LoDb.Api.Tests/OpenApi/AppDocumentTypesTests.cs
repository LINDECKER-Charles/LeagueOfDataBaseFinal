using System.Text.Json.Nodes;
using LoDb.Api.Hosting.OpenApi;
using LoDb.Testing;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace LoDb.Api.Tests.OpenApi;

/// <summary>
/// The <c>app</c> document types what the TypeScript client is generated from: numbers are
/// numbers only, and every enum is a typed one, which the generator exports as an array.
/// </summary>
public sealed class AppDocumentTypesTests
{
    [Fact]
    public async Task NumbersAreNeverAlsoStrings()
    {
        var types = Nodes(await AppDocumentAsync())
            .Select(static node => node["type"])
            .OfType<JsonArray>()
            .Select(static type => type.Select(static t => t?.GetValue<string>()).ToList());

        Assert.DoesNotContain(types, static type =>
            type.Contains("string")
            && (type.Contains("integer") || type.Contains("number")));
    }

    [Fact]
    public async Task EveryEnumIsTyped()
    {
        var document = await AppDocumentAsync();
        var enums = Nodes(document).Where(static node => node["enum"] is JsonArray).ToList();

        Assert.Contains(enums, static node => node["enum"]!.AsArray().Any(static value =>
            value?.GetValue<string>() == "zh-hant"));
        Assert.All(enums, static node => Assert.NotNull(node["type"]));
    }

    private static async Task<JsonNode> AppDocumentAsync()
    {
        await using var factory = new ApiFactory();
        var provider = factory.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(
            OpenApiDocuments.App);
        var document = await provider.GetOpenApiDocumentAsync(
            TestContext.Current.CancellationToken);
        var json = await document.SerializeAsJsonAsync(
            OpenApiSpecVersion.OpenApi3_1,
            TestContext.Current.CancellationToken);
        return JsonNode.Parse(json)!;
    }

    private static IEnumerable<JsonObject> Nodes(JsonNode? node) => node switch
    {
        JsonObject obj => obj.Select(static pair => pair.Value).SelectMany(Nodes).Prepend(obj),
        JsonArray array => array.SelectMany(Nodes),
        _ => [],
    };
}
