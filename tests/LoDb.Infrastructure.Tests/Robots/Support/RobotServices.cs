using LoDb.Infrastructure.Analytics;
using LoDb.Infrastructure.Analytics.Capture;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Robots;
using LoDb.Infrastructure.Robots.Verification;
using LoDb.Infrastructure.Tests.Analytics.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Infrastructure.Tests.Robots.Support;

/// <summary>
/// The robots journal registered as the API registers it, on a database and a clock of the
/// test, behind a resolver of fixed answers.
/// </summary>
internal static class RobotServices
{
    /// <param name="connectionString">The database of the test.</param>
    /// <param name="timeProvider">The clock of every service.</param>
    /// <param name="dns">The answers of the resolver.</param>
    public static ServiceProvider Build(
        string connectionString,
        TimeProvider timeProvider,
        FakeReverseDns dns)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:LoDb"] = connectionString,
                [AnalyticsOptions.SectionName + ":VisitorKey"] = AnalyticsServices.VisitorKey,
                [RobotsOptions.SectionName + ":UndeclaredThreshold"] = "3",
            })
            .Build();
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton(timeProvider)
            .AddFakeLogging()
            .AddMetrics()
            .AddLoDbPersistence(configuration)
            .AddLoDbAnalytics(configuration)
            .AddSingleton<ITrackedPageResolver, StubPageResolver>();
        services.Replace(ServiceDescriptor.Singleton<IReverseDns>(dns));
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
