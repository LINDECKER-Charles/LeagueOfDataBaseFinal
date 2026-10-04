using LoDb.Desktop.Auth.Api;

namespace LoDb.Desktop.Auth;

/// <summary>
/// ProblemDetails of the token endpoints: relayed from the API, or the host's own.
/// </summary>
internal static class ProblemRelay
{
    /// <summary>The API could not be reached: the host answers 502 with this code.</summary>
    public const string ApiUnreachable = "api-unreachable";

    private const string CodeExtension = "code";

    /// <summary>The API's refusal as received, or 502 when it could not be reached.</summary>
    public static IResult FailureOf(AccountCall call) =>
        call.Problem is { } problem
            ? new RelayedProblem(problem)
            : Problem(StatusCodes.Status502BadGateway, ApiUnreachable);

    /// <summary>A ProblemDetails of the host, with a <c>code</c> as the API's have.</summary>
    public static IResult Problem(int status, string code) =>
        TypedResults.Problem(
            statusCode: status,
            extensions: new Dictionary<string, object?> { [CodeExtension] = code });

    private sealed class RelayedProblem(ApiProblem problem) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = problem.Status;
            httpContext.Response.ContentType = problem.ContentType;
            await httpContext.Response.Body.WriteAsync(problem.Body, httpContext.RequestAborted);
        }
    }
}
