using System.Text.Json;

namespace LoDb.Api.Modules.PublicApi.Http;

/// <summary>
/// A JSON answer of <c>/v1</c>, written as go-api's encoder writes it: the value, then a
/// line feed; no body for <c>HEAD</c>.
/// </summary>
internal sealed class V1JsonResult<T>(int statusCode, T value) : IResult
{
    private static readonly byte[] LineFeed = [(byte)'\n'];

    public int StatusCode => statusCode;

    public T Value => value;

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var response = httpContext.Response;
        response.StatusCode = statusCode;
        response.ContentType = V1Json.ContentType;
        if (HttpMethods.IsHead(httpContext.Request.Method))
        {
            return;
        }

        var aborted = httpContext.RequestAborted;
        await JsonSerializer.SerializeAsync(response.Body, value, V1Json.Options, aborted);
        await response.Body.WriteAsync(LineFeed, aborted);
    }
}
