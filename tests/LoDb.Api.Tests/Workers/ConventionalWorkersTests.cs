using LoDb.Api.Tests.Workers.Fake;
using LoDb.Api.Workers;
using LoDb.Api.Workers.Accounts;
using LoDb.Api.Workers.Analytics;
using LoDb.Api.Workers.Audit;
using LoDb.Api.Workers.Billing;
using LoDb.Api.Workers.Catalog;
using LoDb.Api.Workers.Ingestion;
using LoDb.Api.Workers.Outbox;
using LoDb.Api.Workers.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LoDb.Api.Tests.Workers;

/// <summary>
/// Background services are found by the folder convention, and <c>LoDb:Workers:Enabled</c>
/// set to <c>false</c> keeps every one of them from starting.
/// </summary>
public sealed class ConventionalWorkersTests
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public void ConventionFindsTheBackgroundServicesOfTheWorkersFolder()
    {
        var workers = ConventionalWorkers.Discover(typeof(FakeWorker).Assembly);

        Assert.Equal(new[] { typeof(FakeWorker) }, workers);
    }

    // The exact set: a worker gone missing, or an unexpected one, fails alike.
    [Fact]
    public void ApiRunsExactlyItsBackgroundServices()
    {
        Type[] expected =
        [
            typeof(AuthenticatorSecretsJob),
            typeof(RefreshFamilyPurgeJob),
            typeof(UnverifiedAccountPurgeJob),
            typeof(AnalyticsRetentionJob),
            typeof(AnalyticsRollupJob),
            typeof(AnalyticsWriterWorker),
            typeof(AudienceTrendsWorker),
            typeof(AuditRetentionJob),
            typeof(CreditExpiryJob),
            typeof(ArtVariantsJob),
            typeof(LatestCatalogWarmer),
            typeof(OnDemandIngestionWorker),
            typeof(PatchWatchJob),
            typeof(VersionIngestionWorker),
            typeof(EmailOutboxWorker),
            typeof(CspReportSummaryWorker),
        ];

        Assert.Equal(expected, ConventionalWorkers.Discover(typeof(Program).Assembly));
    }

    [Fact]
    public async Task EnabledWorkerStartsWithTheHost()
    {
        var probe = new FakeWorkerProbe();
        using var host = BuildHost(probe, enabled: "true");

        await host.StartAsync(TestContext.Current.CancellationToken);

        await probe.Started.Task.WaitAsync(StartTimeout, TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DisabledWorkerIsNeitherRegisteredNorStarted()
    {
        var probe = new FakeWorkerProbe();
        using var host = BuildHost(probe, enabled: "false");

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(
            host.Services.GetServices<IHostedService>(),
            static service => service is FakeWorker);
        Assert.False(probe.Started.Task.IsCompleted);
    }

    private static IHost BuildHost(FakeWorkerProbe probe, string enabled)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { [ConventionalWorkers.EnabledKey] = enabled });
        builder.Services.AddSingleton(probe);
        builder.Services.AddConventionalWorkers(
            builder.Configuration,
            typeof(FakeWorker).Assembly);
        return builder.Build();
    }
}
