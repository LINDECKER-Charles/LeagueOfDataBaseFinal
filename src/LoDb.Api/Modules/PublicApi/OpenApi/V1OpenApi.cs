using System.Text.Json.Nodes;
using LoDb.Api.Modules.PublicApi.Builds;
using LoDb.Api.Modules.PublicApi.Http;
using LoDb.Api.Modules.PublicApi.Trends;
using Microsoft.OpenApi;

namespace LoDb.Api.Modules.PublicApi.OpenApi;

/// <summary>
/// What the <c>public-v1</c> document says of each route: its answers, and the query
/// parameters its handler reads itself, as go-api reads them.
/// </summary>
internal static class V1OpenApi
{
    private const string TypeParameter = "type";

    /// <summary>The body of a success.</summary>
    public static RouteHandlerBuilder ProducesV1<TResponse>(this RouteHandlerBuilder builder) =>
        builder.Produces<TResponse>(StatusCodes.Status200OK, V1Json.MediaType);

    /// <summary>
    /// The refusals of the gate (the key, the rate limit and the quota, an outage) and the
    /// route's <paramref name="own"/> ones, each with the envelope of <c>/v1</c>.
    /// </summary>
    public static RouteHandlerBuilder ProducesV1Refusals(
        this RouteHandlerBuilder builder,
        params int[] own)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(own);
        int[] gate =
        [
            StatusCodes.Status401Unauthorized,
            StatusCodes.Status403Forbidden,
            StatusCodes.Status429TooManyRequests,
            StatusCodes.Status500InternalServerError,
            StatusCodes.Status503ServiceUnavailable,
        ];
        foreach (var status in own.Concat(gate).Distinct().Order())
        {
            builder.Produces<ErrorEnvelope>(status, V1Json.MediaType);
        }

        return builder;
    }

    /// <summary><c>?page=</c> and <c>?per_page=</c>.</summary>
    public static RouteHandlerBuilder AddPaginationParameters(this RouteHandlerBuilder builder) =>
        builder.AddOpenApiOperationTransformer((operation, _, _) =>
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(Query(
                Pagination.PageParameter,
                "The page, from 1; beyond the last one, an empty page.",
                PositiveInteger(1)));
            operation.Parameters.Add(Query(
                Pagination.PerPageParameter,
                $"Builds per page, from 1; above {Pagination.MaxPerPage}, capped.",
                PositiveInteger(Pagination.DefaultPerPage)));
            return Task.CompletedTask;
        });

    /// <summary>The types of the path, and <c>?range=</c>.</summary>
    public static RouteHandlerBuilder AddTrendsParameters(this RouteHandlerBuilder builder) =>
        builder.AddOpenApiOperationTransformer((operation, _, _) =>
        {
            operation.Parameters ??= [];
            if (operation.Parameters.FirstOrDefault(IsTypeParameter) is OpenApiParameter type)
            {
                type.Schema = Choice(TrendType.All.Select(static kind => kind.Segment));
            }

            var range = Choice(TrendRange.All.Select(static window => window.Label));
            range.Default = JsonValue.Create(TrendRange.Week.Label);
            operation.Parameters.Add(Query(
                TrendsEndpoint.RangeParameter,
                "The window: the last 7 or 30 UTC days, today included.",
                range));
            return Task.CompletedTask;
        });

    private static bool IsTypeParameter(IOpenApiParameter parameter) =>
        string.Equals(parameter.Name, TypeParameter, StringComparison.Ordinal);

    private static OpenApiParameter Query(string name, string description, OpenApiSchema schema) =>
        new()
        {
            Name = name,
            In = ParameterLocation.Query,
            Description = description,
            Schema = schema,
        };

    private static OpenApiSchema PositiveInteger(int fallback) => new()
    {
        Type = JsonSchemaType.Integer,
        Format = "int64",
        Minimum = "1",
        Default = JsonValue.Create(fallback),
    };

    private static OpenApiSchema Choice(IEnumerable<string> values) => new()
    {
        Type = JsonSchemaType.String,
        Enum = [.. values.Select(static value => (JsonNode)JsonValue.Create(value))],
    };
}
