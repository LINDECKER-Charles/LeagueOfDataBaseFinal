using System.Globalization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Hosting.Errors;

/// <summary>
/// Answers a request the database failed: under <c>/api</c>, a pool that stayed busy for its
/// whole wait gets a <c>503</c> with <c>Retry-After</c>, load shed on purpose; anywhere, a
/// server out of reach gets a <c>500</c>, counted, since it is an outage.
/// </summary>
/// <remarks>
/// <c>/v1</c> never gets here: its gate answers with go-api's envelope itself. Outside of
/// <c>/api</c>, a busy pool keeps the default 500 those surfaces already give. Any other
/// exception goes on to the default handler.
/// </remarks>
internal sealed partial class DatabaseFailureHandler(
    DatabaseMetrics metrics,
    IOptions<HostingOptions> options,
    ILogger<DatabaseFailureHandler> logger) : IExceptionHandler
{
    private const string CodeExtension = "code";
    private const string BusyCode = "service-busy";
    private const string BusyTitle = "The service is busy: retry after the Retry-After delay.";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        switch (DatabaseFailures.Of(exception))
        {
            case DatabaseFailure.Unavailable:
                metrics.RecordUnavailable();
                LogUnavailable(logger, exception, RouteOf(httpContext));
                await TypedResults.Problem(statusCode: StatusCodes.Status500InternalServerError)
                    .ExecuteAsync(httpContext);
                return true;
            case DatabaseFailure.PoolExhausted
                when httpContext.Request.Path.StartsWithSegments(ApiPaths.App):
                LogBusy(logger, RouteOf(httpContext));
                await AnswerBusyAsync(httpContext);
                return true;
            default:
                return false;
        }
    }

    private Task AnswerBusyAsync(HttpContext httpContext)
    {
        var delay = options.Value.DatabaseBusyRetryAfter;
        httpContext.Response.Headers.RetryAfter =
            ((long)Math.Ceiling(delay.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        return TypedResults.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: BusyTitle,
                extensions: new Dictionary<string, object?> { [CodeExtension] = BusyCode })
            .ExecuteAsync(httpContext);
    }

    // The route's template, never the path, which may carry a share token.
    private static string? RouteOf(HttpContext httpContext) =>
        (httpContext.Features.Get<IExceptionHandlerFeature>()?.Endpoint as RouteEndpoint)
            ?.RoutePattern.RawText;

    [LoggerMessage(
        EventName = "database.server.unavailable",
        Level = LogLevel.Error,
        Message = "Request of {Route} answered 500: the database is out of reach.")]
    private static partial void LogUnavailable(ILogger logger, Exception exception, string? route);

    // Without the exception: its cause is known, and a stack per shed request would flood
    // the logs exactly when the API is short of capacity.
    [LoggerMessage(
        EventName = "database.pool.exhausted",
        Level = LogLevel.Warning,
        Message = "Request of {Route} answered 503: no database connection freed up in time.")]
    private static partial void LogBusy(ILogger logger, string? route);
}
