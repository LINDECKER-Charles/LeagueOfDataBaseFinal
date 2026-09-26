namespace LoDb.Api.Hosting;

/// <summary>
/// Path prefixes of the two HTTP surfaces: the application API and the public API.
/// </summary>
/// <remarks>
/// CORS, error responses and the OpenAPI documents are split along these prefixes, so a
/// module maps its group under one of them.
/// </remarks>
internal static class ApiPaths
{
    /// <summary>Application API consumed by the web front and the apps (ProblemDetails).</summary>
    public const string App = "/api";

    /// <summary>Public API of the key holders (go-api contract).</summary>
    public const string PublicV1 = "/v1";
}
