using Microsoft.AspNetCore.Diagnostics;

namespace LoDb.Api.Hosting.Errors;

/// <summary>
/// Answers a request the server refused while an endpoint read it, such as a body over the
/// size limit, with the client error the server chose (413, 400, 408) instead of a 500.
/// </summary>
/// <remarks>
/// Endpoints that bind JSON already answer so; one that reads its body itself (the Stripe
/// webhook) would otherwise end in a 500 logged as a fault of the code, and counted with the
/// outages.
/// </remarks>
internal sealed partial class RejectedRequestHandler(ILogger<RejectedRequestHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException rejected)
        {
            return false;
        }

        LogRejected(logger, rejected, rejected.StatusCode);
        await TypedResults.Problem(statusCode: rejected.StatusCode).ExecuteAsync(httpContext);
        return true;
    }

    [LoggerMessage(
        EventName = "http.request.rejected",
        Level = LogLevel.Debug,
        Message = "The server refused the request as it was read: answered {StatusCode}.")]
    private static partial void LogRejected(ILogger logger, Exception exception, int statusCode);
}
