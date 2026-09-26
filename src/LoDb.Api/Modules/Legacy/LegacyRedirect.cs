namespace LoDb.Api.Modules.Legacy;

/// <summary>The 301 of an old URL to its new form.</summary>
/// <remarks>
/// Kept an hour at most, like the host redirect of nginx: a bare 301 is cached almost
/// forever by browsers, while its target may change with the latest version (a page of the
/// latest patch leaves the short URL for a pinned one after the next promotion). Engines
/// consolidate on the status alone.
/// </remarks>
internal sealed record LegacyRedirect(string Location) : IResult
{
    public const string CacheControl = "public, max-age=3600";

    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var response = httpContext.Response;
        response.StatusCode = StatusCodes.Status301MovedPermanently;
        response.Headers.Location = Location;
        response.Headers.CacheControl = CacheControl;
        return Task.CompletedTask;
    }
}
