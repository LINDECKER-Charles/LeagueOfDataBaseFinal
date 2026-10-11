using LoDb.Domain.Versions;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Catalog.Images;

/// <summary>
/// The verdict memos of the versions read lately (<see cref="VersionVerdicts"/>), the least
/// recently read one dropped first, each one for <see cref="CatalogOptions.ImageVerdictsLifetime"/>
/// at most.
/// </summary>
/// <remarks>
/// The lifetime bounds how long a verdict another process replaced (<c>ingest --force</c>)
/// is still served, and how long a key read before its row was written stays pending for
/// a reader of what is stored. The memo is per process: every instance reads the manifest
/// once per key and lifetime.
/// </remarks>
internal sealed class ImageVerdicts
{
    private readonly Lock gate = new();
    private readonly Dictionary<PatchVersion, LinkedListNode<Held>> held = [];
    private readonly LinkedList<Held> recent = new();
    private readonly int capacity;
    private readonly TimeSpan lifetime;
    private readonly TimeProvider timeProvider;

    public ImageVerdicts(IOptions<CatalogOptions> options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        capacity = options.Value.ImageVerdictVersions;
        lifetime = options.Value.ImageVerdictsLifetime;
        this.timeProvider = timeProvider;
    }

    /// <summary>
    /// The memo of <paramref name="version"/>, which becomes the most recently read; a new,
    /// empty one when none is held or the held one has expired.
    /// </summary>
    public VersionVerdicts Of(PatchVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        var now = timeProvider.GetUtcNow();
        lock (gate)
        {
            if (held.TryGetValue(version, out var node) && now - node.Value.Memo.ReadAt < lifetime)
            {
                recent.Remove(node);
                recent.AddFirst(node);
                return node.Value.Memo;
            }

            if (node is not null)
            {
                Drop(node);
            }

            return Hold(version, new VersionVerdicts(now));
        }
    }

    private VersionVerdicts Hold(PatchVersion version, VersionVerdicts memo)
    {
        held[version] = recent.AddFirst(new Held(version, memo));
        while (held.Count > capacity && recent.Last is { } oldest)
        {
            Drop(oldest);
        }

        return memo;
    }

    private void Drop(LinkedListNode<Held> node)
    {
        held.Remove(node.Value.Version);
        recent.Remove(node);
    }

    private sealed record Held(PatchVersion Version, VersionVerdicts Memo);
}
