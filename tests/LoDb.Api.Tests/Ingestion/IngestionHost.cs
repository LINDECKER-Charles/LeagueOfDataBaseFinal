using LoDb.Infrastructure.Jobs;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Storage;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Ddragon;
using LoDb.Ingestion.Egress;
using LoDb.Ingestion.Pipeline;
using LoDb.Testing;
using LoDb.Testing.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Ingestion;

/// <summary>
/// A migrated database and a temporary storage, with the zones of the ingestion over the
/// recorded Data Dragon.
/// </summary>
internal sealed class IngestionHost : IAsyncDisposable
{
    private const string StoragePrefix = "lodb-api-ingestion-tests-";

    private IngestionHost(TestDatabase database, string storageRoot)
    {
        Database = database;
        StorageRoot = storageRoot;
    }

    public static CancellationToken Token => TestContext.Current.CancellationToken;

    public TestDatabase Database { get; }

    public string StorageRoot { get; }

    /// <summary>The settings a container passes after the command words.</summary>
    public IReadOnlyList<string> Settings =>
    [
        $"--ConnectionStrings:LoDb={Database.ConnectionString}",
        $"--LoDb:Storage:Root={StorageRoot}",
        // The production backoff starts at one second: only the attempts matter.
        "--LoDb:Egress:RetryBaseDelay=00:00:00.001",
        "--Logging:LogLevel:Default=Warning",
    ];

    public static async Task<IngestionHost> CreateAsync(PostgresContainerFixture postgres)
    {
        ArgumentNullException.ThrowIfNull(postgres);
        var database = await postgres.CreateDatabaseAsync(Token);
        await database.MigrateAsync(Token);
        var root = Directory.CreateTempSubdirectory(StoragePrefix).FullName;
        return new IngestionHost(database, root);
    }

    /// <summary>The zones Program.cs registers for the ingestion, answered by the replay.</summary>
    public static void AddZones(
        IServiceCollection services,
        IConfiguration configuration,
        FixtureReplayHandler replay) =>
        services
            .AddLoDbEgress(configuration)
            .AddLoDbStorage(configuration)
            .AddLoDbPersistence(configuration)
            .AddLoDbJobs(configuration)
            .AddLoDbDdragon(configuration)
            .AddLoDbIngestion(configuration)
            .AddLoDbCatalog(configuration)
            .AddDdragonFixtureReplay(replay);

    /// <summary>The same zones in a container of their own, as the API host has them.</summary>
    public ServiceProvider BuildServices(FixtureReplayHandler replay)
    {
        var configuration = new ConfigurationBuilder()
            .AddCommandLine([.. Settings])
            .Build();
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging()
            .AddMetrics();
        AddZones(services, configuration, replay);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    /// <summary>The first column of a query, as text.</summary>
    public Task<IReadOnlyList<string>> QueryAsync(string sql) => Database.QueryAsync(sql, Token);

    /// <summary>Files of the storage under <paramref name="relativeDirectory"/>.</summary>
    public int CountFiles(string relativeDirectory)
    {
        var directory = Path.Combine(StorageRoot, relativeDirectory);
        return Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Count()
            : 0;
    }

    public async ValueTask DisposeAsync()
    {
        await Database.DisposeAsync();
        Directory.Delete(StorageRoot, recursive: true);
    }
}
