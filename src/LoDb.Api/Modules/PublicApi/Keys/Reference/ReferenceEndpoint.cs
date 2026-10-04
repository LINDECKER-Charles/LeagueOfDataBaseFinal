using LoDb.Api.Hosting;
using LoDb.Api.Modules.Billing.Catalog;
using LoDb.Api.Modules.Billing.Keys;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.PublicApi.Keys.Reference;

/// <summary>
/// <c>GET /api/public-api/reference</c>: the settings the page <c>/developers</c> documents,
/// public.
/// </summary>
/// <remarks>
/// The base URL is the configured one (<see cref="ReferenceOptions.BaseUrl"/>), else the
/// site's origin, which nginx routes <c>/v1</c> on as well: never the legacy stack's
/// hard-coded one.
/// </remarks>
internal sealed class ReferenceEndpoint(
    IOptions<PublicApiOptions> site,
    IOptions<ReferenceOptions> reference)
{
    public const string Path = ApiPaths.App + "/public-api/reference";

    public const string Tag = "PublicApiReference";

    public static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(
                Path,
                static ([FromServices] ReferenceEndpoint endpoint) => endpoint.Read())
            .WithTags(Tag)
            .WithName("getPublicApiReference")
            .WithSummary("The base URL, key format and offers the public API documents.");

    public Ok<PublicApiReference> Read() => TypedResults.Ok(new PublicApiReference
    {
        BaseUrl = BaseUrl(),
        KeyPrefix = ApiKeySecrets.Prefix,
        FreePlan = new FreePlanTerms
        {
            MonthlyQuota = ApiPlans.FreeQuota,
            RatePerMinute = ApiPlans.FreeRate,
        },
        CreditsRatePerMinute = ApiPlans.CreditsRate,
        Packs = ApiPacks.All,
        Plans = ApiPlans.Subscriptions,
    });

    // An origin, written without its trailing slash so that a path appends to it.
    private string BaseUrl()
    {
        var configured = reference.Value.BaseUrl;
        var origin = string.IsNullOrWhiteSpace(configured) ? site.Value.SiteOrigin : configured;
        return origin.Trim().TrimEnd('/');
    }
}
