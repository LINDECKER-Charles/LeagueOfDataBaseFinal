using LoDb.Api.Modules.PublicApi.Http;

namespace LoDb.Api.Modules.PublicApi.Gate;

/// <summary>
/// Stands before every route of <c>/v1</c>: go-api's 404 for a path it would not route, the
/// admission of the request, and go-api's envelope for whatever fails behind it.
/// </summary>
/// <remarks>
/// An outage of the database or of the storage answers <c>503 internal</c>, anything else
/// <c>500 internal</c>, as go-api does; the <c>X-RateLimit-*</c> headers set by then stay.
/// A request the client gave up on is not answered.
/// </remarks>
internal sealed partial class PublicApiGate(ApiAccess access, ILogger<PublicApiGate> logger)
    : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var http = context.HttpContext;
        if (!RouteShape.IsExact(http))
        {
            return GoMuxText.NotFound;
        }

        try
        {
            var billed = http.GetEndpoint()?.Metadata.GetMetadata<QuotaExemption>() is null;
            return await access.AdmitAsync(http, billed) ?? await next(context);
        }
        catch (Exception exception) when (!http.RequestAborted.IsCancellationRequested)
        {
            var route = (http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
            var keyId = http.Features.Get<ApiCaller>()?.Key.Id;
            if (DependencyFailures.IsOutage(exception))
            {
                LogUnavailable(logger, exception, route, keyId);
                return V1Errors.Unavailable;
            }

            LogFailed(logger, exception, route, keyId);
            return V1Errors.InternalError;
        }
    }

    [LoggerMessage(
        EventName = "publicapi.request.unavailable",
        Level = LogLevel.Error,
        Message = "Request of {Route} (key {ApiKeyId}) answered 503: a dependency failed.")]
    private static partial void LogUnavailable(
        ILogger logger,
        Exception exception,
        string? route,
        int? apiKeyId);

    [LoggerMessage(
        EventName = "publicapi.request.failed",
        Level = LogLevel.Error,
        Message = "Request of {Route} (key {ApiKeyId}) answered 500.")]
    private static partial void LogFailed(
        ILogger logger,
        Exception exception,
        string? route,
        int? apiKeyId);
}
