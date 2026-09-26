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
/// <c>/v1</c> is served by this host on the site's origin (nginx routes it), which
/// <c>LoDb:PublicApi:SiteOrigin</c> sets for the <c>share_url</c> of the builds already: the
/// documentation states the same origin instead of the legacy stack's hard-coded one.
/// </remarks>
internal sealed class ReferenceEndpoint(IOptions<PublicApiOptions> options)
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
        BaseUrl = options.Value.SiteOrigin.TrimEnd('/'),
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
}
