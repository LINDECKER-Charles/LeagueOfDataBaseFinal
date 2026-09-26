using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace LoDb.Api.Modules.ClientPolicy;

/// <summary>The policy the apps read at startup and before offering an update.</summary>
internal static class ClientPolicyEndpoint
{
    public const string Tag = "ClientPolicy";

    // Short: withdrawing a faulty release must reach the apps within a minute.
    private const string CacheControl = "public, max-age=60";

    public static void Map(IEndpointRouteBuilder api) =>
        api.MapGet("/client-policy", Get)
            .WithTags(Tag)
            .WithName("getClientPolicy")
            .WithSummary("Minimum and latest version of each app.");

    private static Ok<AppPolicy> Get(
        [FromServices] IOptionsMonitor<ClientPolicyOptions> options,
        HttpContext httpContext)
    {
        httpContext.Response.Headers[HeaderNames.CacheControl] = CacheControl;
        return TypedResults.Ok(AppPolicy.Of(options.CurrentValue));
    }
}
