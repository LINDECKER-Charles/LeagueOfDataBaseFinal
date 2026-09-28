using LoDb.Infrastructure.Persistence.Ddragon;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LoDb.Ingestion.Tests.Pipeline;

/// <summary>
/// A migrated database, a temporary storage and a wall clock, shared by the API instances a
/// test starts over them.
/// </summary>
internal sealed class IngestionHarness : IAsyncDisposable
{
    public static readonly DateTimeOffset Start = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    private const string StoragePrefix = "lodb-ingestion-tests-";

    private readonly List<IngestionInstance> instances = [];
    private readonly IConfiguration configuration;

    private IngestionHarness(
        TestDatabase database,
        string storageRoot,
        IReadOnlyDictionary<string, string?> settings)
    {
        Database = database;
        StorageRoot = storageRoot;
        configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:LoDb"] = database.ConnectionString,
                ["LoDb:Storage:Root"] = storageRoot,
                // The production backoff starts at one second: only the attempts matter.
                ["LoDb:Egress:RetryBaseDelay"] = "00:00:00.001",
            })
            .AddInMemoryCollection(settings)
            .Build();
    }

    public static CancellationToken Token => TestContext.Current.CancellationToken;

    public TestDatabase Database { get; }

    public string StorageRoot { get; }

    public TestClock Clock { get; } = new(Start);

    public static async Task<IngestionHarness> CreateAsync(
        PostgresContainerFixture postgres,
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        ArgumentNullException.ThrowIfNull(postgres);
        var database = await postgres.CreateDatabaseAsync(Token);
        await database.MigrateAsync(Token);
        var root = Directory.CreateTempSubdirectory(StoragePrefix).FullName;
        return new IngestionHarness(database, root, settings ?? new Dictionary<string, string?>());
    }

    /// <summary>A new API instance over the same database, storage and clock.</summary>
    public IngestionInstance StartInstance()
    {
        var instance = new IngestionInstance(configuration, Clock);
        instances.Add(instance);
        return instance;
    }

    public async Task<DdragonVersion?> VersionRowAsync(string version)
    {
        await using var db = Database.CreateContext();
        return await db.DdragonVersions.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Version == version, Token);
    }

    public async Task<List<DdragonAsset>> AssetRowsAsync(string version)
    {
        await using var db = Database.CreateContext();
        return await db.DdragonAssets.AsNoTracking()
            .Where(row => row.Version == version)
            .ToListAsync(Token);
    }

    /// <summary>Files of the storage under <paramref name="relativeDirectory"/>.</summary>
    public IReadOnlyList<string> FilesUnder(string relativeDirectory)
    {
        var directory = Path.Combine(StorageRoot, relativeDirectory);
        return Directory.Exists(directory)
            ? [.. Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(StorageRoot, path).Replace('\\', '/'))
                .Order(StringComparer.Ordinal)]
            : [];
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var instance in instances)
        {
            await instance.DisposeAsync();
        }

        await Database.DisposeAsync();
        Directory.Delete(StorageRoot, recursive: true);
    }
}
