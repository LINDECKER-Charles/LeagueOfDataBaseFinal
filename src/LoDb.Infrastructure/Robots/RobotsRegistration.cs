using LoDb.Infrastructure.Robots.Capture;
using LoDb.Infrastructure.Robots.Detection;
using LoDb.Infrastructure.Robots.Reports;
using LoDb.Infrastructure.Robots.Reports.Reading;
using LoDb.Infrastructure.Robots.Retention;
using LoDb.Infrastructure.Robots.Verification;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Robots;

/// <summary>
/// Registrations of the robots journal: what nginx reports of the robots, turned into hits,
/// written, kept and reported to the admin.
/// </summary>
/// <remarks>
/// Called by the analytics zone (<see cref="Analytics.AnalyticsRegistration"/>), whose
/// visitor hash keys the pace of a client. No I/O and nothing thrown: the OpenAPI generation
/// builds the host. The API registers the endpoints and the workers.
/// </remarks>
public static class RobotsRegistration
{
    public static IServiceCollection AddLoDbRobots(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<RobotsOptions>()
            .Bind(configuration.GetSection(RobotsOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor
            .Singleton<IValidateOptions<RobotsOptions>, RobotsOptionsValidator>());
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<RobotsMetrics>();
        services.TryAddSingleton<RobotJournalQueue>();
        services.TryAddSingleton<IRobotJournal>(
            static provider => provider.GetRequiredService<RobotJournalQueue>());
        services.TryAddSingleton<IReverseDns, SystemReverseDns>();
        services.TryAddSingleton<NetworkResolver>();
        services.TryAddSingleton<UndeclaredDetector>();
        services.TryAddSingleton<JournalHits>();
        services.TryAddSingleton<IRobotJournalPump, RobotJournalPump>();
        services.TryAddSingleton<RobotDailyReader>();
        services.TryAddSingleton<RobotHitReader>();
        services.TryAddSingleton<IRobotReports, RobotReports>();
        services.TryAddSingleton<IRobotRetention, RobotRetention>();
        return services;
    }
}
