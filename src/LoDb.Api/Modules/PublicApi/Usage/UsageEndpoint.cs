using LoDb.Api.Modules.PublicApi.Gate;
using LoDb.Api.Modules.PublicApi.Http;
using LoDb.Api.Modules.PublicApi.Metering;
using LoDb.Api.Modules.PublicApi.OpenApi;
using LoDb.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.PublicApi.Usage;

/// <summary>
/// <c>GET /v1/usage</c>: the plan and the consumption of the calling key, read afresh from
/// the database.
/// </summary>
/// <remarks>
/// Rate limited but neither charged nor counted: a key whose quota is spent can still read
/// it. The requests of the last second may not be written yet.
/// </remarks>
internal sealed class UsageEndpoint(LoDbDbContext db, UsageCalendar calendar)
{
    public const string Pattern = "/usage";

    public static void Map(IEndpointRouteBuilder v1) =>
        v1.MapRead(
                Pattern,
                static (HttpContext context, [FromServices] UsageEndpoint endpoint) =>
                    endpoint.GetAsync(context))
            .WithName("v1GetUsage")
            .WithSummary("The plan of the key and its consumption this month.")
            .ProducesV1<UsageResponse>()
            .ProducesV1Refusals();

    public async Task<IResult> GetAsync(HttpContext context)
    {
        var keyId = ApiCaller.Of(context).Key.Id;
        var month = calendar.CurrentMonth;
        var usage = await db.ApiKeys
            .AsNoTracking()
            .Where(key => key.Id == keyId)
            .Select(key => new
            {
                key.Plan,
                key.MonthlyQuota,
                key.CreditsBalance,
                key.RateLimitPerMin,
                Used = db.ApiUsage
                    .Where(day => day.ApiKeyId == key.Id && day.Day >= month)
                    .Sum(day => (long?)day.Requests) ?? 0,
            })
            .FirstOrDefaultAsync(context.RequestAborted);

        // Deleted since it was admitted: go-api answers as for an outage.
        if (usage is null)
        {
            throw new DependencyUnavailableException("The key left api_keys.");
        }

        return V1Json.Ok(new UsageResponse(
            usage.Plan,
            usage.MonthlyQuota,
            usage.Used,
            Math.Max(0, usage.MonthlyQuota - usage.Used),
            usage.CreditsBalance,
            usage.RateLimitPerMin));
    }
}
