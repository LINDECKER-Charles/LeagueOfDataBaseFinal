using System.Collections.Concurrent;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Reading;

namespace LoDb.Api.Tests.Prerender.Support;

/// <summary>
/// The older patches and their pages, as the test sets them: by default every version has the
/// same six pages, the four lists and two entities, and is an older patch for every server.
/// </summary>
internal sealed class FakeCatalog : IPrerenderCatalog
{
    public static readonly IReadOnlyList<string> SixPages =
    [
        "champions", "items", "runes", "summoners", "champions/Aatrox", "items/1036-long-sword",
    ];

    private int _pinnedReads;

    /// <summary>The pinned versions, newest first.</summary>
    public List<PatchVersion> Pinned { get; } = [];

    /// <summary>The latest version; none by default.</summary>
    public PatchVersion? Latest { get; set; }

    /// <summary>What a version answers, when not <see cref="SixPages"/>.</summary>
    public Dictionary<PatchVersion, VersionPageList> Pages { get; } = [];

    /// <summary>
    /// From when a pinned version is one for every server, when not from always: a version
    /// that just left the latest rank.
    /// </summary>
    public Dictionary<PatchVersion, DateTimeOffset> PinnedFrom { get; } = [];

    /// <summary>
    /// The reads a version answers not stored yet before its pages: an ingestion under way.
    /// </summary>
    public ConcurrentDictionary<PatchVersion, int> PendingReads { get; } = [];

    /// <summary>The versions whose ingestion was waited for, in order.</summary>
    public ConcurrentQueue<PatchVersion> Ingested { get; } = [];

    /// <summary>
    /// The versions whose pages were read, in order: a read of a version not stored queues its
    /// ingestion.
    /// </summary>
    public ConcurrentQueue<PatchVersion> PageReads { get; } = [];

    /// <summary>How many times the older patches were read.</summary>
    public int PinnedReads => _pinnedReads;

    public Task<IReadOnlyList<PatchVersion>> PinnedVersionsAsync(
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _pinnedReads);
        return Task.FromResult<IReadOnlyList<PatchVersion>>([.. Pinned]);
    }

    public Task<PatchVersion?> LatestVersionAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Latest);

    public Task<DateTimeOffset?> PinnedFromAsync(
        PatchVersion version,
        CancellationToken cancellationToken) =>
        Task.FromResult<DateTimeOffset?>(Pinned.Contains(version)
            ? PinnedFrom.GetValueOrDefault(version, DateTimeOffset.MinValue)
            : null);

    public Task<VersionPageList> PagesAsync(
        PatchVersion version,
        CancellationToken cancellationToken)
    {
        PageReads.Enqueue(version);
        if (PendingReads.TryGetValue(version, out var left) && left > 0)
        {
            PendingReads[version] = left - 1;
            return Task.FromResult(Without(CatalogLoadStatus.Pending));
        }

        return Task.FromResult(Pages.GetValueOrDefault(version) ?? VersionPageList.Ready(SixPages));
    }

    public Task IngestAsync(PatchVersion version, CancellationToken cancellationToken)
    {
        Ingested.Enqueue(version);
        return Task.CompletedTask;
    }

    public static VersionPageList Without(CatalogLoadStatus status) =>
        VersionPageList.Without(status);
}
