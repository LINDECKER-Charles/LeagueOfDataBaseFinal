using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace LoDb.Api.Modules.Catalog.Http;

/// <summary>
/// A 200 JSON answer with its <c>Cache-Control</c> and a strong <c>ETag</c>, or a 304 when
/// the client already holds it.
/// </summary>
/// <remarks>
/// The tag is the SHA-256 of the body as the API serializes it, so two instances tag the same
/// content alike and a list that gained an image gets a new tag. The body is serialized once,
/// before the headers are written, to be hashed.
/// </remarks>
/// <typeparam name="TValue">The documented response schema.</typeparam>
internal sealed class CachedJson<TValue>(TValue value, CacheHeaders cache)
    : IResult, IEndpointMetadataProvider
{
    private const string JsonContentType = "application/json; charset=utf-8";

    public TValue Value => value;

    public CacheHeaders Cache => cache;

    public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Metadata.Add(new ProducesResponseTypeMetadata(
            StatusCodes.Status200OK,
            typeof(TValue),
            ["application/json"]));
        builder.Metadata.Add(new ProducesResponseTypeMetadata(StatusCodes.Status304NotModified));
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var options = httpContext.RequestServices
            .GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
        var body = JsonSerializer.SerializeToUtf8Bytes(value, options);
        var tag = new EntityTagHeaderValue($"\"{Convert.ToBase64String(SHA256.HashData(body))}\"");
        var response = httpContext.Response;
        response.Headers.ETag = tag.ToString();
        response.Headers.CacheControl = cache.CacheControl;
        if (cache.RetryAfter is { } delay)
        {
            response.Headers.RetryAfter = CacheHeaders.Seconds(delay);
        }

        if (IsHeldBy(httpContext.Request, tag))
        {
            response.StatusCode = StatusCodes.Status304NotModified;
            return;
        }

        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = JsonContentType;
        response.ContentLength = body.Length;
        await response.Body.WriteAsync(body, httpContext.RequestAborted);
    }

    // If-None-Match compares weakly (RFC 9110 § 13.1.2): a proxy may have weakened the tag.
    private static bool IsHeldBy(HttpRequest request, EntityTagHeaderValue tag) =>
        request.GetTypedHeaders().IfNoneMatch.Any(held =>
            held.Equals(EntityTagHeaderValue.Any) || held.Compare(tag, useStrongComparison: false));
}
