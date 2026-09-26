namespace LoDb.Api.Hosting;

/// <summary>
/// Named rate limiting policies of the API.
/// </summary>
/// <remarks>
/// The rate limiter is wired into the host from the start (services and middleware, after
/// authorization), so the security chantier only declares its policies here. A module may
/// also add its own through <c>services.Configure&lt;RateLimiterOptions&gt;</c>.
/// </remarks>
internal static class RateLimitingPolicies
{
    public static IServiceCollection AddLoDbRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddRateLimiter(static options =>
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests);
}
