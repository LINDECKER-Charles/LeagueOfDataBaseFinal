using System.Globalization;
using LoDb.Api.Modules.Admin.Prerender.Revision.Drain;
using LoDb.Api.Modules.Admin.Prerender.Revision.Producing;
using LoDb.Api.Tests.Prerender.Support;
using LoDb.Domain.Catalog;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.Ddragon;
using LoDb.Infrastructure.Persistence.Prerender.Revision;
using LoDb.Ingestion.Catalog.States.Snapshots;
using LoDb.Testing;
using LoDb.Testing.States;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.Prerender.Revision;

/// <summary>
/// The queue of the closed states on the pass's harness: four covered versions, 16.8.1 to
/// 16.11.1, the last two promoted (16.11.1 the latest, so 16.10.1 the version it replaced),
/// the three older ones pinned; an item whose state the promotion of 16.11.1 closed.
/// </summary>
internal sealed class RevisionRig : IAsyncDisposable
{
    public static readonly PatchVersion Oldest = PatchVersion.Parse("16.8.1");
    public static readonly PatchVersion Older = PatchVersion.Parse("16.9.1");
    public static readonly PatchVersion Leaving = PatchVersion.Parse("16.10.1");
    public static readonly PatchVersion Latest = PatchVersion.Parse("16.11.1");
    public static readonly IReadOnlyList<string> Covered =
        ["16.8.1", "16.9.1", "16.10.1", "16.11.1"];

    /// <summary>An item a promotion changed: one of its states closed.</summary>
    public static readonly IndexedEntity Item = new(ResourceType.Items, "1036");

    /// <summary>When the generation of the rig was built; its states closed after it.</summary>
    public static readonly DateTimeOffset BuiltAt = TestStates.BuiltAt;

    private RevisionRig(PrerenderHarness harness) => Harness = harness;

    public PrerenderHarness Harness { get; }

    public RevisionProducer Producer => Harness.Get<RevisionProducer>();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static async Task<RevisionRig> StartAsync(
        PostgresContainerFixture postgres,
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        var harness = await PrerenderHarness.StartAsync(postgres, settings: settings);
        harness.Catalog.Pinned.AddRange([Leaving, Older, Oldest]);
        harness.Catalog.Latest = Latest;
        await using var db = harness.Database.CreateContext();
        db.DdragonVersions.AddRange(Promoted(Leaving, 2), Promoted(Latest, 1));
        await db.SaveChangesAsync(Token);
        return new RevisionRig(harness);
    }

    /// <summary>A state of <see cref="Item"/>, closed at <paramref name="closedAt"/>.</summary>
    public static IndexedState Closed(
        string opener,
        string last,
        DateTimeOffset closedAt,
        IndexedEntity? entity = null) =>
        TestStates.Closed(entity ?? Item, opener, last) with
        {
            Path = $"items/{(entity ?? Item).Id}-long-sword",
            ClosedAt = closedAt,
        };

    /// <summary>Generation <paramref name="generationId"/> over <see cref="Covered"/>.</summary>
    public void Index(long generationId, params IndexedState[] states) =>
        Harness.Index.Snapshot = EntityStateSnapshot.Of(
            new StateIndexGeneration
            {
                Id = generationId,
                Scheme = 1,
                BuiltAt = BuiltAt,
                Versions = [.. Covered.Select(PatchVersion.Parse)],
            },
            states,
            []);

    /// <summary>
    /// The first run of the producer, on generation 1: its history is never enqueued.
    /// </summary>
    public async Task SettleAsync()
    {
        Index(1);
        await ProduceAsync();
    }

    /// <summary>A run of the producer on the index as the test set it; the rows written.</summary>
    public Task<int> ProduceAsync() => Producer.ProduceAsync(Harness.Index.Snapshot, Token);

    public async Task<List<PrerenderRevisionPage>> RowsAsync()
    {
        await using var db = Harness.Database.CreateContext();
        return await db.PrerenderRevisions.AsNoTracking()
            .OrderBy(static page => page.Epoch)
            .ThenBy(static page => page.Path)
            .ToListAsync(Token);
    }

    public async Task<PrerenderRevisionWatermark?> WatermarkAsync()
    {
        await using var db = Harness.Database.CreateContext();
        return await db.PrerenderRevisionWatermarks.AsNoTracking().SingleOrDefaultAsync(Token);
    }

    public Task DrainAsync() => Harness.Get<RevisionDrain>().RunAsync(Token);

    public ValueTask DisposeAsync() => Harness.DisposeAsync();

    /// <summary>A setting of the pre-rendering, as the hosts write it.</summary>
    public static KeyValuePair<string, string?> Setting(string key, int value) =>
        new($"LoDb:Prerender:{key}", value.ToString(CultureInfo.InvariantCulture));

    private static DdragonVersion Promoted(PatchVersion version, int daysAgo)
    {
        var promotedAt = DateTimeOffset.UtcNow.AddDays(-daysAgo);
        return new DdragonVersion
        {
            Version = version.Value,
            Status = DdragonVersionStatus.Ready,
            DiscoveredAt = promotedAt,
            UpdatedAt = promotedAt,
            ReadyAt = promotedAt,
            PromotedAt = promotedAt,
        };
    }
}
