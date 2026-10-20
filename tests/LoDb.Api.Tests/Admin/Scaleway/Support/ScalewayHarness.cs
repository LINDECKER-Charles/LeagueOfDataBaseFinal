using LoDb.Api.Modules.Admin.Scaleway;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Api.Tests.Admin.Scaleway.Support;

/// <summary>
/// The bill's registration as the API makes it, its client answered by
/// <see cref="FakeScaleway"/>, a clock that only moves when a test advances it, and every log
/// line kept, the most verbose included.
/// </summary>
internal sealed class ScalewayHarness : IDisposable
{
    /// <summary>The key of the tests, which no log line may hold.</summary>
    public const string Key = "scw-billing-key-of-the-tests-5f1c2a";

    private const string Section = "LoDb:ScalewayBilling:";

    private readonly ServiceProvider _services;

    public ScalewayHarness(bool isConfigured = true)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(isConfigured ? Settings() : [])
            .Build();
        var services = new ServiceCollection()
            .AddLogging(static logging => logging
                .SetMinimumLevel(LogLevel.Trace)
                .AddFakeLogging())
            .AddSingleton<TimeProvider>(Clock)
            .AddAdminScalewayBilling(configuration);
        services.AddHttpClient(ScalewayBillingClient.ClientName)
            .ConfigurePrimaryHttpMessageHandler(() => Scaleway);
        _services = services.BuildServiceProvider();
    }

    public FakeScaleway Scaleway { get; } = new();

    public FakeTimeProvider Clock { get; } =
        new(new DateTimeOffset(2026, 10, 20, 18, 0, 0, TimeSpan.Zero));

    public ScalewayBillReader Reader => _services.GetRequiredService<ScalewayBillReader>();

    public FakeLogCollector Logs => _services.GetFakeLogCollector();

    /// <summary>The settings of a configured bill, under their configuration keys.</summary>
    public static Dictionary<string, string?> Settings() => new()
    {
        [Section + "SecretKey"] = Key,
        [Section + "OrganizationId"] = ScalewayAnswers.OrganizationId,
        [Section + "BackupsProjectId"] = ScalewayAnswers.BackupsProjectId,
        [Section + "PagesProjectId"] = ScalewayAnswers.PagesProjectId,
    };

    public void Dispose()
    {
        _services.Dispose();
        Scaleway.Dispose();
    }
}
