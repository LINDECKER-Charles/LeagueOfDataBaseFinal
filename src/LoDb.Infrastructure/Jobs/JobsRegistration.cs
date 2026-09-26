using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Infrastructure.Jobs;

/// <summary>
/// Registrations of the jobs zone: distributed locks and the base of periodic jobs.
/// </summary>
/// <remarks>
/// Program.cs calls it from the start and the zone's owner fills it, so no shared file
/// changes. It must neither do I/O nor throw: the OpenAPI generation builds the host.
/// </remarks>
public static class JobsRegistration
{
    public static IServiceCollection AddLoDbJobs(
        this IServiceCollection services,
        IConfiguration configuration) => services;
}
