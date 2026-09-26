using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace LoDb.Api.Hosting.OpenApi;

/// <summary>
/// The two OpenAPI documents: <c>app</c> for <c>/api</c>, source of the TypeScript client,
/// and <c>public-v1</c> for <c>/v1</c>, source of the developers page.
/// </summary>
/// <remarks>
/// A module refines its document through <c>services.Configure&lt;OpenApiOptions&gt;</c>
/// with the document name, never by editing this file.
/// </remarks>
internal static class OpenApiDocuments
{
    public const string App = "app";
    public const string PublicV1 = "public-v1";
    private const string AppPrefix = "api/";
    private const string PublicV1Prefix = "v1/";
    private const string GeneratorAssembly = "GetDocument.Insider";

    /// <summary>
    /// True inside the build-time generator, which starts the host with a no-op server:
    /// background services must then stay off.
    /// </summary>
    public static bool IsBuildTimeGeneration { get; } = string.Equals(
        Assembly.GetEntryAssembly()?.GetName().Name,
        GeneratorAssembly,
        StringComparison.Ordinal);

    public static IServiceCollection AddLoDbOpenApi(this IServiceCollection services) =>
        services
            .AddOpenApi(App, static options =>
                options.ShouldInclude = static api => Includes(AppPrefix, api))
            .AddOpenApi(PublicV1, static options =>
                options.ShouldInclude = static api => Includes(PublicV1Prefix, api));

    /// <summary>Whether an endpoint's route starts with the document's path prefix.</summary>
    internal static bool Includes(string prefix, ApiDescription api) =>
        api.RelativePath?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true;
}
