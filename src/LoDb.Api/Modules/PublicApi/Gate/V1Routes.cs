using LoDb.Api.Modules.PublicApi.Http;

namespace LoDb.Api.Modules.PublicApi.Gate;

/// <summary>
/// Maps the routes of <c>/v1</c> as go-api's router serves them: <c>GET</c> with its
/// <c>HEAD</c>, a plain-text 405 for any other method, and a plain-text 404 for a path no
/// route spells.
/// </summary>
internal static class V1Routes
{
    private const string AnyPath = "/{**path}";

    // The reads come first; then the other methods of their paths; then every other path.
    private const int OtherMethodsOrder = 1;
    private const int OtherPathsOrder = 2;

    /// <summary>A read, for <c>GET</c> and <c>HEAD</c>; the document lists the first.</summary>
    public static RouteHandlerBuilder MapRead(
        this IEndpointRouteBuilder routes,
        string pattern,
        Delegate handler)
    {
        ArgumentNullException.ThrowIfNull(routes);
        routes.MapMethods(pattern, [HttpMethods.Head], handler).ExcludeFromDescription();
        return routes.MapGet(pattern, handler);
    }

    /// <summary>
    /// go-api's answers to what no read serves under <paramref name="v1"/>: the other methods
    /// of the <paramref name="patterns"/> of the reads, then every other path.
    /// </summary>
    public static void MapRouterAnswers(
        this IEndpointRouteBuilder v1,
        IEnumerable<string> patterns)
    {
        ArgumentNullException.ThrowIfNull(v1);
        ArgumentNullException.ThrowIfNull(patterns);
        foreach (var pattern in patterns)
        {
            v1.Map(pattern, (RequestDelegate)AnswerOtherMethodAsync)
                .WithOrder(OtherMethodsOrder)
                .ExcludeFromDescription();
        }

        v1.Map(AnyPath, (RequestDelegate)GoMuxText.NotFound.ExecuteAsync)
            .WithOrder(OtherPathsOrder)
            .ExcludeFromDescription();
    }

    // A path spelled otherwise than its route is unknown to go-api, whatever the method.
    private static Task AnswerOtherMethodAsync(HttpContext context) =>
        (RouteShape.IsExact(context) ? GoMuxText.MethodNotAllowed : GoMuxText.NotFound)
            .ExecuteAsync(context);
}
