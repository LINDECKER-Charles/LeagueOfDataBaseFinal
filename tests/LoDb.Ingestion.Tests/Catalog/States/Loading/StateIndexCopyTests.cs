using System.Diagnostics.Metrics;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.States;
using LoDb.Ingestion.Catalog.States.Loading;
using LoDb.Ingestion.Catalog.States.Snapshots;
using LoDb.Ingestion.Catalog.States.Telemetry;
using LoDb.Ingestion.Tests.Catalog.States.Building;
using LoDb.Ingestion.Tests.Pipeline;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace LoDb.Ingestion.Tests.Catalog.States.Loading;

/// <summary>
/// The copy of the index a process holds: loaded once, checked by its stamp at most every
/// versions' lifetime, at once after a write of its own, kept when the database fails, and
/// unavailable only without a copy.
/// </summary>
public sealed class StateIndexCopyTests(PostgresContainerFixture postgres)
{
    private const string First = "90.1.1";
    private const string Second = "90.2.1";
    private const string Loaded = "states.copy.loaded";

    private static readonly TimeSpan Lifetime = TimeSpan.FromMilliseconds(300);

    private static readonly ServiceProvider Meters =
        new ServiceCollection().AddMetrics().BuildServiceProvider();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ReadsShareOneLoadUntilThisProcessWritesTheIndex()
    {
        await using var index = await StateIndexHarness.CreateAsync(postgres, First, Second);
        await StoreAsync(index);
        await index.PromoteAndListenAsync(First);
        var copy = index.Instance.Get<IEntityStateIndex>();

        var first = await copy.GetAsync(Token);
        var again = await copy.GetAsync(Token);
        await index.PromoteAndListenAsync(Second);
        var extended = await copy.GetAsync(Token);

        Assert.Same(first, again);
        Assert.Equal([First], first.Coverage.All.Select(static version => version.Value));
        Assert.Equal(PatchVersion.Parse(Second), extended.Coverage.End);
        Assert.Equal(2, LoadsOf(index.Instance));
    }

    // The ingest command can promote: a process that did not write sees it within a lifetime.
    [Fact]
    public async Task AnotherProcessWriteShowsOnceTheLifetimeIsOver()
    {
        var settings = new Dictionary<string, string?>
        {
            ["LoDb:Catalog:VersionsLifetime"] = Lifetime.ToString("c"),
        };
        await using var index = await StateIndexHarness.CreateAsync(
            postgres, settings, [First, Second]);
        await StoreAsync(index);
        await index.PromoteAndListenAsync(First);
        var reader = index.StartInstance().Get<IEntityStateIndex>();
        var before = await reader.GetAsync(Token);

        await index.PromoteAndListenAsync(Second);
        await Task.Delay(Lifetime * 2, Token);
        var after = await reader.GetAsync(Token);

        Assert.Equal(PatchVersion.Parse(First), before.Coverage.End);
        Assert.Equal(PatchVersion.Parse(Second), after.Coverage.End);
    }

    [Fact]
    public async Task NothingBuiltReadsEmpty()
    {
        await using var index = await StateIndexHarness.CreateAsync(postgres, First);

        var snapshot = await index.Instance.Get<IEntityStateIndex>().GetAsync(Token);

        Assert.Same(EntityStateSnapshot.Empty, snapshot);
    }

    [Fact]
    public async Task UnreachableDatabaseKeepsTheCopyHeld()
    {
        await using var database = await postgres.CreateMigratedDatabaseAsync(Token);
        var contexts = new Contexts(database);
        var copy = CopyOver(contexts, TimeSpan.Zero);
        var held = await copy.GetAsync(Token);

        contexts.IsBroken = true;
        var kept = await copy.GetAsync(Token);

        Assert.Same(held, kept);
    }

    [Fact]
    public async Task UnreachableDatabaseWithoutACopyIsUnavailable()
    {
        await using var database = await postgres.CreateMigratedDatabaseAsync(Token);
        var copy = CopyOver(new Contexts(database) { IsBroken = true }, TimeSpan.Zero);

        await Assert.ThrowsAsync<StateIndexUnavailableException>(
            async () => await copy.GetAsync(Token));
    }

    private static async Task StoreAsync(StateIndexHarness index)
    {
        var datasets = await ReducedDatasets.ReadAsync();
        await index.StoreAsync(First, datasets);
        await index.StoreAsync(Second, datasets.WithoutItem(ReducedDatasets.Potion));
    }

    private static int LoadsOf(IngestionInstance instance) =>
        instance.Logs.Count(static log => log.Id.Name == Loaded);

    private static StateIndexCopy CopyOver(Contexts contexts, TimeSpan lifetime) => new(
        new StoredEntityStateIndex(contexts),
        new StateIndexStamps(contexts),
        Options.Create(new CatalogOptions { VersionsLifetime = lifetime }),
        new StateIndexMetrics(Meters.GetRequiredService<IMeterFactory>()),
        TimeProvider.System,
        NullLogger<StateIndexCopy>.Instance);

    // The test's database, or a server nobody listens on once broken.
    private sealed class Contexts(TestDatabase database) : IDbContextFactory<LoDbDbContext>
    {
        private static readonly NpgsqlDataSource Nowhere =
            NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Timeout=1");

        public bool IsBroken { get; set; }

        public LoDbDbContext CreateDbContext()
        {
            if (!IsBroken)
            {
                return database.CreateContext();
            }

            var options = new DbContextOptionsBuilder<LoDbDbContext>();
            options.UseLoDb(Nowhere);
            return new LoDbDbContext(options.Options);
        }
    }
}
