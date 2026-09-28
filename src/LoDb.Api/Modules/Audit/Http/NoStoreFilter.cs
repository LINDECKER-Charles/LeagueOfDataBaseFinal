namespace LoDb.Api.Modules.Audit.Http;

/// <summary>
/// Keeps every audit answer out of the caches: they hold addresses and follow the session.
/// </summary>
internal sealed class NoStoreFilter : IEndpointFilter
{
    private const string NoStore = "no-store";

    public ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        context.HttpContext.Response.Headers.CacheControl = NoStore;
        return next(context);
    }
}
