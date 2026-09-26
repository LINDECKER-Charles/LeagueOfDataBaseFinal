namespace LoDb.Api.Modules.Catalog.Http;

/// <summary>
/// A failed catalog call, written as a ProblemDetails whose <c>code</c> extension a client
/// switches on; a <c>Retry-After</c> comes with the failures a later call may not meet.
/// </summary>
internal sealed record CatalogProblem : IResult
{
    private const string CodeExtension = "code";

    public required int Status { get; init; }

    /// <summary>Stable kebab-case code, the contract a client relies on.</summary>
    public required string Code { get; init; }

    public required string Title { get; init; }

    public string? Detail { get; init; }

    public TimeSpan? RetryAfter { get; init; }

    public static CatalogProblem InvalidVersion(string? version) => BadRequest(
        "invalid-version",
        "The version does not match the version pattern.",
        $"'{version}' is not a version such as 16.19.1.");

    public static CatalogProblem InvalidLanguage(string? language) => BadRequest(
        "invalid-language",
        "The language does not match the language pattern.",
        $"'{language}' is not a Data Dragon language such as en_US.");

    public static CatalogProblem InvalidPage() => BadRequest(
        "invalid-page",
        "The page or its size is out of range.",
        $"page starts at 1 and size lies between 1 and {PageRequest.MaxSize}.");

    public static CatalogProblem InvalidQuery(string detail) =>
        BadRequest("invalid-query", "The search query is not valid.", detail);

    public static CatalogProblem UnknownVersion(string version) => NotFound(
        "unknown-version",
        "Data Dragon does not list this version.",
        $"Version {version} does not exist.");

    public static CatalogProblem UnknownLanguage(string language) => NotFound(
        "unknown-language",
        "Data Dragon does not list this language for the version.",
        $"Language {language} does not exist for this version.");

    public static CatalogProblem UnknownEntity(string segment, string id) => NotFound(
        "unknown-entity",
        "The catalog holds no such entry.",
        $"No {segment} '{id}' in this version.");

    /// <summary>The catalog is queued for ingestion, or the queue refused it.</summary>
    public static CatalogProblem Pending() => Unavailable(
        "catalog-pending",
        "The catalog of this version is not ingested yet.");

    public static CatalogProblem UpstreamUnavailable() => Unavailable(
        "upstream-unavailable",
        "Data Dragon could not be reached.");

    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        if (RetryAfter is { } delay)
        {
            httpContext.Response.Headers.RetryAfter = CacheHeaders.Seconds(delay);
        }

        var problem = TypedResults.Problem(
            detail: Detail,
            statusCode: Status,
            title: Title,
            extensions: new Dictionary<string, object?> { [CodeExtension] = Code });
        return problem.ExecuteAsync(httpContext);
    }

    private static CatalogProblem BadRequest(string code, string title, string detail) => new()
    {
        Status = StatusCodes.Status400BadRequest,
        Code = code,
        Title = title,
        Detail = detail,
    };

    private static CatalogProblem NotFound(string code, string title, string detail) => new()
    {
        Status = StatusCodes.Status404NotFound,
        Code = code,
        Title = title,
        Detail = detail,
    };

    private static CatalogProblem Unavailable(string code, string title) => new()
    {
        Status = StatusCodes.Status503ServiceUnavailable,
        Code = code,
        Title = title,
        RetryAfter = CacheHeaders.RetryDelay,
    };
}
