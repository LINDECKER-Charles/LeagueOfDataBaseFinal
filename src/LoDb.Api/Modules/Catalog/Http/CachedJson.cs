using System.Reflection;
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
/// The tag is that of the <see cref="JsonBody"/>. The body is serialized once, before the
/// headers are written, to be hashed; an endpoint that memoises its body passes it instead.
/// </remarks>
/// <typeparam name="TValue">The documented response schema.</typeparam>
/// <param name="value">The answer.</param>
/// <param name="cache">How long it may be reused.</param>
/// <param name="body">
/// <paramref name="value"/> already serialized with the API's options; null to serialize it.
/// </param>
internal sealed class CachedJson<TValue>(TValue value, CacheHeaders cache, JsonBody? body = null)
    : IResult, IEndpointMetadataProvider
{
    private const string JsonContentType = "application/json; charset=utf-8";

    public TValue Value => value;

    public CacheHeaders Cache => cache;

    /// <summary>The body the endpoint serialized ahead, if any.</summary>
    public JsonBody? Body => body;

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
        var written = body ?? JsonBody.Of(value, httpContext.RequestServices
            .GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions);
        var tag = written.Tag;
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
        response.ContentLength = written.Bytes.Length;
        await response.Body.WriteAsync(written.Bytes, httpContext.RequestAborted);
    }

    // If-None-Match compares weakly (RFC 9110 § 13.1.2): a proxy may have weakened the tag.
    private static bool IsHeldBy(HttpRequest request, EntityTagHeaderValue tag) =>
        request.GetTypedHeaders().IfNoneMatch.Any(held =>
            held.Equals(EntityTagHeaderValue.Any) || held.Compare(tag, useStrongComparison: false));
}
