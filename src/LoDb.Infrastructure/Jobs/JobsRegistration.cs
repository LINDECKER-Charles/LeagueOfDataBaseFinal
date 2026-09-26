using LoDb.Infrastructure.Locks;
using LoDb.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Infrastructure.Jobs;

/// <summary>
/// Registrations of the jobs zone: distributed locks and the base of periodic jobs.
/// </summary>
/// <remarks>
/// Program.cs calls it from the start and the zone's owner fills it, so no shared file
/// changes. It must neither do I/O nor throw: the OpenAPI generation builds the host. The
/// concrete jobs are registered by the worker convention of the API.
/// </remarks>
public static class JobsRegistration
{
    public static IServiceCollection AddLoDbJobs(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddSingleton<IDistributedLock>(static provider =>
            new PostgresDistributedLock(
                LoDbDataSource.ConnectionString(provider.GetRequiredService<IConfiguration>())));
        services.TryAddSingleton<IJobSchedule, PostgresJobSchedule>();
        services.TryAddSingleton<JobMetrics>();
        services.TryAddSingleton<PeriodicJobServices>();
        return services;
    }
}
