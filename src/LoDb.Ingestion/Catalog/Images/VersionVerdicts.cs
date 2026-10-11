using System.Collections.Concurrent;
using LoDb.Infrastructure.Persistence.Ddragon;
using LoDb.Infrastructure.Storage.Blobs;
using LoDb.Ingestion.Images;

namespace LoDb.Ingestion.Catalog.Images;

/// <summary>
/// The manifest verdicts of one version a process has read so far, by manifest key: a blob,
/// an absence, or the note that the key had no row when it was read.
/// </summary>
/// <remarks>
/// Only the keys asked for are held: a version's ~2 000 rows are never read whole, so a
/// crawl that touches each page of many versions once costs the database no more than
/// without the memo. A recorded verdict never changes in the process (an ingestion keeps the
/// rows it finds); only <c>ingest --force</c>, another process, replaces some, which the
/// lifetime of <see cref="ImageVerdicts"/> covers.
/// </remarks>
internal sealed class VersionVerdicts(DateTimeOffset readAt)
{
    private readonly ConcurrentDictionary<(string Type, string Key), ResolvedImage> verdicts =
        new();

    /// <summary>When the first verdict was read: the memo expires from it.</summary>
    public DateTimeOffset ReadAt => readAt;

    /// <summary>
    /// The memoised resolution of a key: a verdict, or <see cref="ResolvedImage.Pending"/> for
    /// a key that had no row, which only a reader of what is stored takes as it is.
    /// </summary>
    /// <returns><see langword="null"/> when the key must be read from the manifest.</returns>
    public ResolvedImage? Lookup((string Type, string Key) key, bool acceptsUnrecorded) =>
        verdicts.TryGetValue(key, out var held)
        && (acceptsUnrecorded || held.Status != ImageStatus.Pending)
            ? held
            : null;

    /// <summary>
    /// Holds what the manifest answered for <paramref name="asked"/>: the rows found, and
    /// the absence of a row for the others, which never hides a verdict read meanwhile.
    /// </summary>
    public void Record(
        IEnumerable<DdragonImage> asked,
        IReadOnlyDictionary<(string Type, string Key), DdragonAsset> rows)
    {
        ArgumentNullException.ThrowIfNull(asked);
        ArgumentNullException.ThrowIfNull(rows);
        foreach (var image in asked)
        {
            var key = (image.ManifestType, image.ManifestKey);
            if (rows.TryGetValue(key, out var row))
            {
                verdicts[key] = VerdictOf(row);
            }
            else
            {
                verdicts.TryAdd(key, ResolvedImage.Pending);
            }
        }
    }

    /// <summary>The resolution held for a key, pending when none is.</summary>
    public ResolvedImage Get((string Type, string Key) key) =>
        verdicts.GetValueOrDefault(key, ResolvedImage.Pending);

    private static ResolvedImage VerdictOf(DdragonAsset row) =>
        row is { Status: DdragonAssetStatus.Present, Sha256: { } sha, Extension: { } extension }
            ? ResolvedImage.Present(new BlobKey(sha, extension))
            : ResolvedImage.Absent;
}
