using LoDb.Api.Modules.Admin.Prerender;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Passes;
using LoDb.Api.Modules.Admin.Prerender.Rendering;
using LoDb.Api.Modules.Admin.Prerender.Writing;
using LoDb.Infrastructure.Jobs;
using LoDb.Infrastructure.PageStore;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace LoDb.Api.Tests.Prerender.Support;

/// <summary>
/// The pass as the API registers it, on a database of its own: its SSR, its catalog and, unless
/// a real one is given, its store replaced by fakes; its clock never waits.
/// </summary>
internal sealed class PrerenderHarness : IAsyncDisposable
{
    public const string PublicHost = "league-of-data-base.com";
    public const string SsrBaseUrl = "http://lodb-web-ssr:4000";
    private const string Section = "LoDb:Prerender";

    private readonly ServiceProvider _services;

    private PrerenderHarness(TestDatabase database, ServiceProvider services, FakeParts parts)
    {
        Database = database;
        _services = services;
        Ssr = parts.Ssr;
        Catalog = parts.Catalog;
        Clock = parts.Clock;
        Store = parts.Store;
    }

    public TestDatabase Database { get; }

    /// <summary>The services, as a command of the command line gets them.</summary>
    public IServiceProvider Services => _services;

    public FakeSsr Ssr { get; }

    public FakeCatalog Catalog { get; }

    public InstantClock Clock { get; }

    /// <summary>The store in memory; null when the test gave a real one.</summary>
    public FakePageStore? Store { get; }

    public FakeLogCollector Logs => _services.GetFakeLogCollector();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A pass over a migrated database, configured for <see cref="PublicHost"/>; on
    /// <paramref name="store"/> (a real store's settings) or a store in memory, with
    /// <paramref name="settings"/> over the others.
    /// </summary>
    public static async Task<PrerenderHarness> StartAsync(
        PostgresContainerFixture postgres,
        IReadOnlyDictionary<string, string?>? store = null,
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        ArgumentNullException.ThrowIfNull(postgres);
        var database = await postgres.CreateMigratedDatabaseAsync(Token);
        var parts = new FakeParts(store is null ? new FakePageStore() : null);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(Settings(database, store))
            .AddInMemoryCollection(settings ?? new Dictionary<string, string?>())
            .Build();
        return new PrerenderHarness(database, Provide(configuration, parts), parts);
    }

    public T Get<T>()
        where T : notnull => _services.GetRequiredService<T>();

    /// <summary>Opens a pass over <paramref name="scope"/>, writing epoch 1.</summary>
    public async Task<long> LaunchAsync(PassScope scope)
    {
        var launch = new PassLaunch { Epoch = 1, Scope = scope };
        return await Get<PassLauncher>().StartAsync(launch, Token)
            ?? throw new InvalidOperationException("A pass is already active.");
    }

    /// <summary>One tick of the worker: the running pass, written as far as it goes.</summary>
    public Task RunAsync() => Get<PrerenderEngine>().RunAsync(Token);

    public async Task<PrerenderPass> PassAsync(long passId)
    {
        await using var db = Database.CreateContext();
        return await db.PrerenderPasses.AsNoTracking()
            .Include(static pass => pass.Units)
            .SingleAsync(pass => pass.Id == passId, Token);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await Database.DisposeAsync();
    }

    private static Dictionary<string, string?> Settings(
        TestDatabase database,
        IReadOnlyDictionary<string, string?>? store)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:LoDb"] = database.AppConnectionString,
            [$"{Section}:PublicHost"] = PublicHost,
            [$"{Section}:SsrBaseUrl"] = SsrBaseUrl,
            [$"{Section}:RendersPerMinute"] = "6000",
            [$"{Section}:Endpoint"] = "https://s3.fr-par.scw.cloud",
            [$"{Section}:Bucket"] = "lodb-pages-test",
            [$"{Section}:AccessKey"] = "writer",
            [$"{Section}:SecretKey"] = "writer-secret",
        };
        foreach (var (key, value) in store ?? new Dictionary<string, string?>())
        {
            settings[key] = value;
        }

        return settings;
    }

    private static ServiceProvider Provide(IConfiguration configuration, FakeParts parts)
    {
        var services = new ServiceCollection()
            .AddSingleton(configuration)
            .AddLogging(static logging => logging
                .SetMinimumLevel(LogLevel.Debug)
                .AddFakeLogging())
            .AddMetrics()
            .AddSingleton<TimeProvider>(parts.Clock)
            .AddLoDbPersistence(configuration)
            .AddLoDbJobs(configuration)
            .AddAdminPrerender(configuration);
        services.Replace(ServiceDescriptor.Singleton<IPrerenderCatalog>(parts.Catalog));
        if (parts.Store is { } store)
        {
            services.Replace(ServiceDescriptor.Singleton<IPageStore>(store));
        }

        services.AddHttpClient(SsrClient.ClientName)
            .ConfigurePrimaryHttpMessageHandler(() => parts.Ssr);
        return services.BuildServiceProvider();
    }

    private sealed record FakeParts(FakePageStore? Store)
    {
        public FakeSsr Ssr { get; } = new();

        public FakeCatalog Catalog { get; } = new();

        public InstantClock Clock { get; } = new();
    }
}
