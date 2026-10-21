using LoDb.Api.Modules.Admin.Prerender;
using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Writing;
using LoDb.Api.Tests.Prerender.Support;
using LoDb.Api.Workers.Prerender;
using LoDb.Infrastructure.Locks;
using LoDb.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Api.Tests.Prerender;

/// <summary>
/// Unset, the pass sleeps without a word; set, its settings are checked when the host starts:
/// a bare public host, a base URL for the SSR, a store reached over https.
/// </summary>
public sealed class PrerenderSettingsTests
{
    private const string Section = "LoDb:Prerender:";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string, string> InvalidSettings => new()
    {
        { "PublicHost", "https://league-of-data-base.com" },
        { "PublicHost", "league-of-data-base.com/fr" },
        { "SsrBaseUrl", "http://lodb-web-ssr:4000/fr" },
        { "SsrBaseUrl", "ftp://lodb-web-ssr" },
        { "ServedEpoch", "0" },
        { "RendersPerMinute", "0" },
        { "MaxInFlight", "0" },
        { "MaxInFlight", "41" },
        { "Renderers", "0" },
        { "Renderers", "9" },
        { "Pace:ManualMaxRendersPerMinute", "0" },
        { "Pace:ManualMaxRendersPerMinute", "6001" },
        { "Pace:RendererCpus", "0" },
        { "Endpoint", "http://s3.fr-par.scw.cloud" },
        { "Pace:MinRendersPerMinute", "0" },
        { "Pace:MinRendersPerMinute", "241" },
        { "Pace:MaxRendersPerMinute", "6001" },
        { "Pace:StepRendersPerMinute", "0" },
        { "Pace:StepRendersPerMinute", "241" },
        { "RendersPerMinute", "241" },
        { "Pace:VisitorsLoadUrl", "http://lodb-web-ssr:9464" },
        { "Pace:VisitorsLoadUrl", "http://user:secret@lodb-web-ssr:9464/load" },
        { "Pace:PassLoadUrl", "ftp://lodb-web-ssr-prerender:9464/load" },
        { "Pace:PassLoadUrl", "http://lodb-web-ssr-prerender:9464/load?all=1" },
        { "Pace:HostCpuSet", "3-1" },
    };

    [Fact]
    public void EmptySettingsAreValidAndLeaveThePassDormant()
    {
        using var services = Services(new Dictionary<string, string?>());

        var activation = services.GetRequiredService<PrerenderActivation>();

        Assert.False(activation.IsActive);
        Assert.False(activation.EnqueuesPromotions);
        var options = services.GetRequiredService<IOptions<PrerenderOptions>>().Value;
        Assert.Equal(1, options.ServedEpoch);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("localhost:18080")]
    [InlineData("test.league-of-data-base.com")]
    public void PublicHostIsAHostWithAPortAtMost(string host)
    {
        using var services = Services(Configured(("PublicHost", host)));

        Assert.True(services.GetRequiredService<PrerenderActivation>().IsActive);
    }

    [Theory]
    [MemberData(nameof(InvalidSettings))]
    public void InvalidSettingIsRefusedAtStartup(string key, string value)
    {
        using var services = Services(Configured((key, value)));

        Assert.Throws<OptionsValidationException>(() =>
            services.GetRequiredService<PrerenderActivation>().IsActive);
    }

    // The hosts' settings: a pace that holds together, both SSRs' load, a bench's CPU list.
    [Fact]
    public void AdaptivePaceWithinItsBoundsIsAccepted()
    {
        using var services = Services(Configured(
            ("RendersPerMinute", "120"),
            ("Pace:MinRendersPerMinute", "30"),
            ("Pace:MaxRendersPerMinute", "120"),
            ("Pace:StepRendersPerMinute", "120"),
            ("Pace:VisitorsLoadUrl", "http://lodb-web-ssr:9464/load"),
            ("Pace:PassLoadUrl", "http://lodb-web-ssr-prerender:9464/load"),
            ("Pace:HostCpuSet", "0,2-3")));

        Assert.True(services.GetRequiredService<PrerenderActivation>().IsActive);
        var pace = services.GetRequiredService<IOptions<PrerenderOptions>>().Value.Pace;
        Assert.Equal((PaceMode.Adaptive, 30, 120), (pace.Mode, pace.MinRendersPerMinute,
            pace.MaxRendersPerMinute));
    }

    [Fact]
    public void PlainHttpStoreIsForTheDevelopmentStackAlone()
    {
        using var services = Services(Configured(
            ("Endpoint", "http://lodb-pages-store:5000"),
            ("AllowInsecureEndpoint", "true")));

        Assert.True(services.GetRequiredService<PrerenderActivation>().IsActive);
    }

    [Fact]
    public async Task UnsetEngineNeverTakesItsLock()
    {
        var locks = new CountingLock();
        using var services = Services(new Dictionary<string, string?>(), locks);

        await services.GetRequiredService<PrerenderEngine>().RunAsync(Token);

        Assert.Equal(0, locks.Asked);
    }

    [Fact]
    public async Task WorkerSleepsWhileTheModuleIsNotRegistered()
    {
        var logs = new FakeLogCollector();
        await using var empty = new ServiceCollection().BuildServiceProvider();
        using var worker = new PrerenderWorker(
            empty.GetRequiredService<IServiceScopeFactory>(),
            new FakeTimeProvider(),
            new FakeLogger<PrerenderWorker>(logs));

        await worker.StartAsync(Token);
        await worker.StopAsync(Token);

        Assert.False(worker.ExecuteTask?.IsFaulted);
        Assert.Empty(logs.GetSnapshot());
    }

    [Fact]
    public async Task PaceWorkerSleepsWhileTheModuleIsNotRegistered()
    {
        var logs = new FakeLogCollector();
        await using var empty = new ServiceCollection().BuildServiceProvider();
        using var worker = new PrerenderPaceWorker(
            empty.GetRequiredService<IServiceScopeFactory>(),
            new FakeTimeProvider(),
            new FakeLogger<PrerenderPaceWorker>(logs));

        await worker.StartAsync(Token);
        await worker.StopAsync(Token);

        Assert.False(worker.ExecuteTask?.IsFaulted);
        Assert.Empty(logs.GetSnapshot());
    }

    private static Dictionary<string, string?> Configured(
        params (string Key, string Value)[] overrides)
    {
        var settings = new Dictionary<string, string?>
        {
            [Section + "PublicHost"] = "league-of-data-base.com",
            [Section + "SsrBaseUrl"] = "http://lodb-web-ssr:4000",
            [Section + "Endpoint"] = "https://s3.fr-par.scw.cloud",
            [Section + "Bucket"] = "lodb-pages-prod",
            [Section + "AccessKey"] = "writer",
            [Section + "SecretKey"] = "writer-secret",
        };
        foreach (var (key, value) in overrides)
        {
            settings[Section + key] = value;
        }

        return settings;
    }

    // The registration as the API makes it, on a database never reached; the catalog in
    // memory, and a lock that counts who asks for it.
    private static ServiceProvider Services(
        Dictionary<string, string?> settings,
        IDistributedLock? locks = null)
    {
        settings["ConnectionStrings:LoDb"] = "Host=lodb-postgres.invalid;Database=unused";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging()
            .AddMetrics()
            .AddLoDbPersistence(configuration)
            .AddAdminPrerender(configuration);
        services.TryAddSingleton(locks ?? new CountingLock());
        services.Replace(ServiceDescriptor.Singleton<IPrerenderCatalog>(new FakeCatalog()));
        return services.BuildServiceProvider();
    }

    private sealed class CountingLock : IDistributedLock
    {
        public int Asked { get; private set; }

        public Task<IAsyncDisposable?> TryAcquireAsync(
            string name,
            CancellationToken cancellationToken)
        {
            Asked++;
            return Task.FromResult<IAsyncDisposable?>(null);
        }
    }
}
