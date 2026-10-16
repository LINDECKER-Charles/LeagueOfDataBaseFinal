namespace LoDb.Api.Modules.Legacy;

/// <summary>
/// The redirect of an old URL: a 301 to its new form, or a 302 to the list of its version
/// when no stored catalog can tell the entry it named.
/// </summary>
/// <remarks>
/// Kept an hour at most, like the host redirect of nginx: a bare 301 is cached almost
/// forever by browsers, while its target may change with the latest version (an entry the
/// next promotion retires leaves the short URL for a pinned one). Engines
/// consolidate on the status alone. The 302 is temporary on purpose: once the version is
/// stored, the same old URL may find its entry, and an entry gone from the game never turns
/// into a 404.
/// </remarks>
internal sealed record LegacyRedirect : IResult
{
    public const string CacheControl = "public, max-age=3600";

    private LegacyRedirect(string location, int status)
    {
        Location = location;
        Status = status;
    }

    public string Location { get; }

    public int Status { get; }

    /// <summary>The new form of the old URL.</summary>
    public static LegacyRedirect Permanent(string location) =>
        new(location, StatusCodes.Status301MovedPermanently);

    /// <summary>The list standing in for an entry no stored catalog can tell.</summary>
    public static LegacyRedirect ToList(string location) =>
        new(location, StatusCodes.Status302Found);

    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var response = httpContext.Response;
        response.StatusCode = Status;
        response.Headers.Location = Location;
        response.Headers.CacheControl = CacheControl;
        return Task.CompletedTask;
    }
}
