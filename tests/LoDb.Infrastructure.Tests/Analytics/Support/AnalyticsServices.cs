using LoDb.Infrastructure.Analytics;
using LoDb.Infrastructure.Analytics.Capture;
using LoDb.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Infrastructure.Tests.Analytics.Support;

/// <summary>
/// The persistence and analytics zones registered as the API registers them, with
/// <see cref="StubPageResolver"/> in place of the catalog's resolver and logs kept in memory.
/// </summary>
internal static class AnalyticsServices
{
    /// <summary>The legacy stack's secret in the sample (<c>Legacy/visitors.json</c>).</summary>
    public const string VisitorKey = "legacy-app-secret";

    /// <param name="connectionString">The database of the test.</param>
    /// <param name="timeProvider">The clock of every service.</param>
    /// <param name="configure">Changes to the options, applied after the settings.</param>
    public static ServiceProvider Build(
        string connectionString,
        TimeProvider timeProvider,
        Action<AnalyticsOptions>? configure = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:LoDb"] = connectionString,
                [AnalyticsOptions.SectionName + ":VisitorKey"] = VisitorKey,
            })
            .Build();
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton(timeProvider)
            .AddFakeLogging()
            .AddMetrics()
            .AddLoDbPersistence(configuration)
            .AddLoDbAnalytics(configuration)
            .AddSingleton<ITrackedPageResolver, StubPageResolver>()
            .Configure(configure ?? (static _ => { }));
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
