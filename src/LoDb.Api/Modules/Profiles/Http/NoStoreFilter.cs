namespace LoDb.Api.Modules.Profiles.Http;

/// <summary>
/// Keeps every profile answer out of the caches: they follow the cookies, and a public card
/// must vanish as soon as its owner hides it or is banned.
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
