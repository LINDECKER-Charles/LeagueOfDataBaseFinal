using LoDb.Infrastructure.Analytics.Capture;
using LoDb.Infrastructure.Analytics.Classification;
using LoDb.Infrastructure.Analytics.Geo;
using LoDb.Infrastructure.Analytics.Import;
using LoDb.Infrastructure.Analytics.Reports;
using LoDb.Infrastructure.Analytics.Retention;
using LoDb.Infrastructure.Analytics.Rollup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Analytics;

/// <summary>
/// Registrations of the analytics zone: capture storage and aggregation.
/// </summary>
/// <remarks>
/// Program.cs calls it from the start and the zone's owner fills it, so no shared file
/// changes. It must neither do I/O nor throw: the OpenAPI generation builds the host. The
/// zone builds on the persistence zone (the data source, the partitions, the event writer)
/// and on the host's metrics; the API registers the <see cref="ITrackedPageResolver"/>, the
/// endpoints and the workers.
/// </remarks>
public static class AnalyticsRegistration
{
    public static IServiceCollection AddLoDbAnalytics(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AnalyticsOptions>()
            .Bind(configuration.GetSection(AnalyticsOptions.SectionName))
            .PostConfigure(options => options.GeoIpDatabase = string.IsNullOrWhiteSpace(
                options.GeoIpDatabase)
                ? configuration[AnalyticsOptions.LegacyGeoIpKey]
                : options.GeoIpDatabase)
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor
            .Singleton<IValidateOptions<AnalyticsOptions>, AnalyticsOptionsValidator>());

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<AnalyticsMetrics>();
        AddCapture(services);
        services.TryAddSingleton<DayEvents>();
        services.TryAddSingleton<DailyStore>();
        services.TryAddSingleton<IAnalyticsRollup, AnalyticsRollup>();
        services.TryAddSingleton<IAnalyticsReports, AnalyticsReports>();
        services.TryAddSingleton<IAnalyticsRetention, AnalyticsRetention>();
        services.TryAddSingleton<ILegacyDailyImport, LegacyDailyImport>();
        return services;
    }

    private static void AddCapture(IServiceCollection services)
    {
        services.TryAddSingleton<PageViewQueue>();
        services.TryAddSingleton<IPageViewCapture>(
            static provider => provider.GetRequiredService<PageViewQueue>());
        services.TryAddSingleton<VisitorHasher>();
        services.TryAddSingleton<IGeoLocator, MaxMindGeoLocator>();
        services.TryAddSingleton<ViewEventFactory>();
        services.TryAddSingleton<PageViewBatchWriter>();
        services.TryAddSingleton<IPageViewPump, PageViewPump>();
    }
}
