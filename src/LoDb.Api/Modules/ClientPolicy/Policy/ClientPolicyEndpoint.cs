using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace LoDb.Api.Modules.ClientPolicy.Policy;

/// <summary>The policy the apps read at startup and before offering an update.</summary>
/// <remarks>
/// An app below the minimum still reads it: the version gate lets this path through, so the
/// app learns the release, or the bundle, that it must update to.
/// </remarks>
internal static class ClientPolicyEndpoint
{
    // Short: withdrawing a faulty release must reach the apps within a minute.
    private const string CacheControl = "public, max-age=60";

    public static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(ClientPolicyRoutes.Policy, GetAsync)
            .WithTags(ClientPolicyRoutes.Tag)
            .WithName("getClientPolicy")
            .WithSummary("Minimum and latest version of each app, and Android's live bundle.");

    private static async Task<Ok<AppPolicy>> GetAsync(
        [FromServices] ClientPolicyStore store,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var policy = await store.ReadAsync(cancellationToken);
        httpContext.Response.Headers[HeaderNames.CacheControl] = CacheControl;
        return TypedResults.Ok(policy);
    }
}
