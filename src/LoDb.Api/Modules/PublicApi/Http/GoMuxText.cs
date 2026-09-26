using Microsoft.Net.Http.Headers;

namespace LoDb.Api.Modules.PublicApi.Http;

/// <summary>
/// The two plain-text answers of go-api's router, byte for byte: a path no route spells
/// exactly, and a method a route does not serve.
/// </summary>
internal sealed class GoMuxText : IResult
{
    private const string TextContentType = "text/plain; charset=utf-8";
    private const string NoSniff = "nosniff";

    private GoMuxText(int statusCode, string text, string? allow)
    {
        StatusCode = statusCode;
        Text = text;
        Allow = allow;
    }

    public static GoMuxText NotFound { get; } = new(
        StatusCodes.Status404NotFound,
        "404 page not found\n",
        allow: null);

    /// <summary>Every route of <c>/v1</c> reads: <c>GET</c>, and <c>HEAD</c> with it.</summary>
    public static GoMuxText MethodNotAllowed { get; } = new(
        StatusCodes.Status405MethodNotAllowed,
        "Method Not Allowed\n",
        allow: "GET, HEAD");

    public int StatusCode { get; }

    public string Text { get; }

    public string? Allow { get; }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var response = httpContext.Response;
        response.StatusCode = StatusCode;
        response.ContentType = TextContentType;
        response.Headers[HeaderNames.XContentTypeOptions] = NoSniff;
        if (Allow is not null)
        {
            response.Headers.Allow = Allow;
        }

        return HttpMethods.IsHead(httpContext.Request.Method)
            ? Task.CompletedTask
            : response.WriteAsync(Text, httpContext.RequestAborted);
    }
}
