using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Api.Hosting.Errors;

/// <summary>
/// The exception handlers tried before the default one, which answers a 500 ProblemDetails.
/// </summary>
internal static class ErrorHandlingRegistration
{
    public static IServiceCollection AddLoDbErrorHandling(this IServiceCollection services)
    {
        services.TryAddSingleton<DatabaseMetrics>();
        services.AddExceptionHandler<DatabaseFailureHandler>();
        return services;
    }
}
