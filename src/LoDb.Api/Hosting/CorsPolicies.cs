using Microsoft.Net.Http.Headers;

namespace LoDb.Api.Hosting;

/// <summary>
/// Cross-origin rules: <c>/api</c> is closed except to the Android app, <c>/v1</c> is open
/// for reading, exactly like go-api.
/// </summary>
internal static class CorsPolicies
{
    /// <summary>Policy of <see cref="ApiPaths.App"/>.</summary>
    public const string AppPolicy = "lodb-app";

    // The Android app serves its bundle from this origin (Capacitor, ADR 0007).
    private const string AndroidOrigin = "https://localhost";
    private const string PublicAllowedOrigin = "*";
    private const string PublicAllowedMethods = "GET, OPTIONS";
    private const string PublicAllowedHeaders = "Authorization, X-Api-Key";

    public static IServiceCollection AddLoDbCors(this IServiceCollection services) =>
        services.AddCors(static options => options.AddPolicy(AppPolicy, static policy => policy
            .WithOrigins(AndroidOrigin)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .WithExposedHeaders(HeaderNames.RetryAfter)));

    public static IApplicationBuilder UseLoDbCors(this IApplicationBuilder app)
    {
        app.UseWhen(
            static context => context.Request.Path.StartsWithSegments(ApiPaths.App),
            static api => api.UseCors(AppPolicy));
        return app.Use(static (context, next) => ApplyPublicApiRules(context, next));
    }

    // go-api answers every /v1/ request with these headers, preflight or not, and answers
    // OPTIONS itself: the /v1 contract tests compare them byte for byte.
    private static Task ApplyPublicApiRules(HttpContext context, RequestDelegate next)
    {
        if (!IsPublicApi(context.Request.Path))
        {
            return next(context);
        }

        // Set when the response starts, so an error response rewritten downstream keeps them.
        context.Response.OnStarting(
            static state => AddPublicHeaders((HttpResponse)state),
            context.Response);
        if (!HttpMethods.IsOptions(context.Request.Method))
        {
            return next(context);
        }

        context.Response.StatusCode = StatusCodes.Status204NoContent;
        return Task.CompletedTask;
    }

    private static Task AddPublicHeaders(HttpResponse response)
    {
        response.Headers.AccessControlAllowOrigin = PublicAllowedOrigin;
        response.Headers.AccessControlAllowMethods = PublicAllowedMethods;
        response.Headers.AccessControlAllowHeaders = PublicAllowedHeaders;
        return Task.CompletedTask;
    }

    // Same test as go-api's case-sensitive "/v1/" prefix: "/v1" alone and "/V1/..." are out.
    private static bool IsPublicApi(PathString path) =>
        path.StartsWithSegments(ApiPaths.PublicV1, StringComparison.Ordinal, out var rest)
        && rest.HasValue;
}
