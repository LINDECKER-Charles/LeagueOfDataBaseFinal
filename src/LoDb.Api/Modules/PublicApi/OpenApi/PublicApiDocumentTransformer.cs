using LoDb.Api.Modules.PublicApi.Access;
using LoDb.Api.Modules.PublicApi.Limits;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LoDb.Api.Modules.PublicApi.OpenApi;

/// <summary>
/// Completes the <c>public-v1</c> document: the two ways of presenting a key, required by
/// every operation, and the <c>X-RateLimit-*</c> headers of the answers given once the key
/// is known.
/// </summary>
internal sealed class PublicApiDocumentTransformer : IOpenApiDocumentTransformer
{
    private const string BearerScheme = "bearer";
    private const string ApiKeyScheme = "apiKey";

    private const string Description =
        "Read-only access to the public data of League of Data Base: profiles, builds and "
        + "trends. Every request presents a key, as a bearer token or in X-Api-Key; its plan "
        + "sets a monthly quota, then prepaid credits pay, and a rate limit per minute. "
        + "Refusals read {\"error\": {\"code\", \"message\"}}.";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Info.Description = Description;
        document.AddComponent<IOpenApiSecurityScheme>(BearerScheme, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = BearerScheme,
            Description = "The key as a bearer token: Authorization: Bearer lodb_…",
        });
        document.AddComponent<IOpenApiSecurityScheme>(ApiKeyScheme, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = ApiKeyCredential.ApiKeyHeader,
            Description = "The key in its own header, read when no bearer token is sent.",
        });

        var operations = document.Paths.Values
            .Where(static path => path.Operations is not null)
            .SelectMany(static path => path.Operations!.Values);
        foreach (var operation in operations)
        {
            // Either scheme: two requirements, not one requiring both.
            operation.Security =
            [
                Requirement(BearerScheme, document),
                Requirement(ApiKeyScheme, document),
            ];
            AddRateLimitHeaders(operation);
        }

        return Task.CompletedTask;
    }

    private static OpenApiSecurityRequirement Requirement(
        string scheme,
        OpenApiDocument document) =>
        new() { [new OpenApiSecuritySchemeReference(scheme, document)] = [] };

    // Every answer but the refusals of the key itself, which is not known yet.
    private static void AddRateLimitHeaders(OpenApiOperation operation)
    {
        if (operation.Responses is null)
        {
            return;
        }

        foreach (var (status, response) in operation.Responses)
        {
            if (status is "401" or "403" || response is not OpenApiResponse answer)
            {
                continue;
            }

            answer.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal);
            answer.Headers[RateLimitVerdict.LimitHeader] = Header(
                "Requests the key may make per minute, in bursts of as many.");
            answer.Headers[RateLimitVerdict.RemainingHeader] = Header(
                "Requests left in the current burst.");
            answer.Headers[RateLimitVerdict.ResetHeader] = Header(
                "Unix time when the burst is whole again; after a 429, when a request is.");
        }
    }

    private static OpenApiHeader Header(string description) => new()
    {
        Description = description,
        Schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int64" },
    };
}
