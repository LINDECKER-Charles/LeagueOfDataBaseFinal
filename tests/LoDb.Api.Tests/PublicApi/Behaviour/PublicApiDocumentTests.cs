using LoDb.Api.Hosting.OpenApi;
using LoDb.Testing;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace LoDb.Api.Tests.PublicApi.Behaviour;

/// <summary>
/// The <c>public-v1</c> document describes what a key holder may call: the four reads, the
/// two ways of presenting a key, the rate limit headers and the parameters go-api reads.
/// </summary>
public sealed class PublicApiDocumentTests
{
    private const string LimitHeader = "X-RateLimit-Limit";

    [Fact]
    public async Task TheFourReadsAreDocumentedOnce()
    {
        var document = await DocumentAsync();

        Assert.Equal(
            [
                "/v1/champions/{championId}/builds",
                "/v1/profiles/{username}",
                "/v1/trends/{type}",
                "/v1/usage",
            ],
            document.Paths.Keys.Order(StringComparer.Ordinal));
        Assert.All(
            document.Paths.Values,
            static path => Assert.Equal([HttpMethod.Get], path.Operations!.Keys));
    }

    [Fact]
    public async Task EveryOperationTakesEitherWayOfPresentingAKey()
    {
        var document = await DocumentAsync();

        var schemes = document.Components!.SecuritySchemes!;
        var bearer = schemes["bearer"];
        var apiKey = schemes["apiKey"];
        Assert.Equal(SecuritySchemeType.Http, bearer.Type);
        Assert.Equal("bearer", bearer.Scheme);
        Assert.Equal(SecuritySchemeType.ApiKey, apiKey.Type);
        Assert.Equal(ParameterLocation.Header, apiKey.In);
        Assert.Equal("X-Api-Key", apiKey.Name);
        Assert.All(
            Operations(document),
            static operation => Assert.Equal(["bearer", "apiKey"], SchemesOf(operation)));
    }

    [Fact]
    public async Task EveryAnswerOnceTheKeyIsKnownCarriesTheRateLimit()
    {
        var document = await DocumentAsync();

        foreach (var operation in Operations(document))
        {
            foreach (var (status, response) in operation.Responses!)
            {
                var limited = response.Headers?.ContainsKey(LimitHeader) ?? false;
                Assert.True(
                    limited == status is not ("401" or "403"),
                    $"{operation.OperationId} {status}: {LimitHeader} {limited}");
            }
        }
    }

    [Fact]
    public async Task EveryRefusalReadsAsTheEnvelope()
    {
        var document = await DocumentAsync();

        var refusals = Operations(document)
            .SelectMany(static operation => operation.Responses!)
            .Where(static response => response.Key != "200")
            .ToList();

        Assert.NotEmpty(refusals);
        Assert.All(refusals, static refusal => Assert.Contains(
            "error",
            refusal.Value.Content!["application/json"].Schema!.Properties!.Keys));
    }

    [Fact]
    public async Task TheParametersGoApiReadsAreDocumented()
    {
        var document = await DocumentAsync();

        var builds = Operation(document, "/v1/champions/{championId}/builds");
        var trends = Operation(document, "/v1/trends/{type}");

        Assert.Equal(
            ["championId", "page", "per_page"],
            builds.Parameters!.Select(static parameter => parameter.Name));
        Assert.Equal(["champions", "items", "runes", "summoners"], Choices(trends, "type"));
        Assert.Equal(["7d", "30d"], Choices(trends, "range"));
    }

    private static async Task<OpenApiDocument> DocumentAsync()
    {
        await using var factory = new ApiFactory();
        var provider = factory.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(
            OpenApiDocuments.PublicV1);
        return await provider.GetOpenApiDocumentAsync(TestContext.Current.CancellationToken);
    }

    private static IEnumerable<OpenApiOperation> Operations(OpenApiDocument document) =>
        document.Paths.Values.SelectMany(static path => path.Operations!.Values);

    // Two requirements of one scheme each: either will do.
    private static IEnumerable<string?> SchemesOf(OpenApiOperation operation) =>
        operation.Security!.Select(static requirement => requirement.Keys.Single().Reference.Id);

    private static OpenApiOperation Operation(OpenApiDocument document, string path) =>
        document.Paths[path].Operations![HttpMethod.Get];

    private static IEnumerable<string> Choices(OpenApiOperation operation, string name) =>
        operation.Parameters!.Single(parameter => parameter.Name == name).Schema!.Enum!
            .Select(static value => value.GetValue<string>());
}
