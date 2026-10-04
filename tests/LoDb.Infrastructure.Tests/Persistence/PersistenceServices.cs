using LoDb.Infrastructure.Jobs;
using LoDb.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Infrastructure.Tests.Persistence;

/// <summary>The persistence and jobs zones registered as the API registers them.</summary>
internal static class PersistenceServices
{
    public static ServiceProvider Build(string? connectionString, TimeProvider timeProvider)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:LoDb"] = connectionString,
            })
            .Build();
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton(timeProvider)
            .AddLogging()
            .AddMetrics()
            .AddLoDbPersistence(configuration)
            .AddLoDbJobs(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
