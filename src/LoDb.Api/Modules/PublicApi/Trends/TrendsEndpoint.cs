using LoDb.Api.Modules.PublicApi.Gate;
using LoDb.Api.Modules.PublicApi.Http;
using LoDb.Api.Modules.PublicApi.OpenApi;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.PublicApi.Trends;

/// <summary>
/// <c>GET /v1/trends/{type}</c>: the most viewed champions, items, runes or summoner spells
/// of the site over the last 7 or 30 days.
/// </summary>
/// <remarks>The window is checked before the type, as go-api checks them.</remarks>
internal sealed class TrendsEndpoint(TrendsService trends)
{
    public const string Pattern = "/trends/{type}";
    public const string RangeParameter = "range";

    private static readonly V1Error InvalidRange = new(
        StatusCodes.Status400BadRequest,
        V1Errors.InvalidRequest,
        TrendRange.UnknownMessage);

    private static readonly V1Error UnknownType = new(
        StatusCodes.Status404NotFound,
        V1Errors.NotFound,
        TrendType.UnknownMessage);

    public static void Map(IEndpointRouteBuilder v1) =>
        v1.MapRead(
                Pattern,
                static (
                    string type,
                    HttpContext context,
                    [FromServices] TrendsEndpoint endpoint) =>
                    endpoint.GetAsync(type, context))
            .WithName("v1GetTrends")
            .WithSummary("The 25 most viewed entities of a type over the last 7 or 30 days.")
            .AddTrendsParameters()
            .ProducesV1<TrendsResponse>()
            .ProducesV1Refusals(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound);

    public async Task<IResult> GetAsync(string type, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var label = GoQuery.Get(context.Request.QueryString, RangeParameter);
        var range = label.Length == 0 ? TrendRange.Week : TrendRange.Find(label);
        if (range is null)
        {
            return InvalidRange;
        }

        if (TrendType.Find(type) is not { } trendType)
        {
            return UnknownType;
        }

        var ranking = await trends.RankAsync(trendType, range, context.RequestAborted);
        return V1Json.Ok(new TrendsResponse(type, range.Label, ranking.Entries));
    }
}
