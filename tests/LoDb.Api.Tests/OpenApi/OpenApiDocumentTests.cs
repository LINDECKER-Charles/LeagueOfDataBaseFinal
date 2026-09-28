using System.Net;
using LoDb.Api.Hosting.OpenApi;
using LoDb.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.OpenApi;

/// <summary>
/// Both documents are generated on demand from the running host, and each one only takes the
/// endpoints of its own path prefix.
/// </summary>
public sealed class OpenApiDocumentTests
{
    [Theory]
    [InlineData(OpenApiDocuments.App)]
    [InlineData(OpenApiDocuments.PublicV1)]
    public async Task DocumentIsGeneratedOnDemand(string documentName)
    {
        await using var factory = new ApiFactory();
        var provider = factory.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(
            documentName);

        var document = await provider.GetOpenApiDocumentAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal($"LoDb.Api | {documentName}", document.Info.Title);
        Assert.DoesNotContain(document.Paths.Keys, static path =>
            path.StartsWith("/healthz", StringComparison.Ordinal)
            || path.StartsWith("/readyz", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Development", HttpStatusCode.OK)]
    [InlineData("Production", HttpStatusCode.NotFound)]
    public async Task DocumentsAreServedOverHttpInDevelopmentOnly(
        string environment,
        HttpStatusCode expected)
    {
        await using var factory = new ApiFactory();
        using var client = factory
            .WithWebHostBuilder(builder => builder.UseEnvironment(environment))
            .CreateClient();

        foreach (var name in new[] { OpenApiDocuments.App, OpenApiDocuments.PublicV1 })
        {
            using var response = await client.GetAsync(
                new Uri($"/openapi/{name}.json", UriKind.Relative),
                TestContext.Current.CancellationToken);
            Assert.Equal(expected, response.StatusCode);
        }
    }

    [Theory]
    [InlineData("api/", "api/catalog/champions", true)]
    [InlineData("api/", "API/catalog/champions", true)]
    [InlineData("api/", "v1/profiles/{username}", false)]
    [InlineData("api/", "apikeys", false)]
    [InlineData("v1/", "v1/profiles/{username}", true)]
    [InlineData("v1/", "api/catalog/champions", false)]
    [InlineData("v1/", null, false)]
    public void DocumentTakesOnlyItsPathPrefix(string prefix, string? route, bool included)
    {
        var api = new ApiDescription { RelativePath = route };

        Assert.Equal(included, OpenApiDocuments.Includes(prefix, api));
    }
}
