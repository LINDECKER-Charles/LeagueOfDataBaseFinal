using LoDb.Parity.Comparison;
using LoDb.Parity.Deviations;

namespace LoDb.Parity.Manifests;

/// <summary>
/// Compares two manifests key by key, on the verdict a page gets: the same blob (SHA-256
/// and extension) for a present image, absent on both sides for a placeholder.
/// </summary>
public static class ManifestComparer
{
    private const string BlobPrefix = "cdn/blobs/";

    private static readonly IReadOnlySet<string> NoTags = new HashSet<string>();

    public static IReadOnlyList<Deviation> Compare(ManifestPair pair)
    {
        ArgumentNullException.ThrowIfNull(pair);
        var keys = pair.Legacy.Keys.Union(pair.Next.Keys).Order(StringComparer.Ordinal);
        return [.. keys.Select(key => Compare(pair, key)).OfType<Deviation>()];
    }

    /// <summary><c>present {sha}.{ext}</c> or <c>absent</c>, from a legacy value.</summary>
    public static string LegacyVerdict(string? path) => path switch
    {
        null => ImageVerdict.Absent,
        _ when path.StartsWith(BlobPrefix, StringComparison.Ordinal) =>
            $"{ImageVerdict.Present} {path[BlobPrefix.Length..]}",
        _ => $"{ImageVerdict.Present} {path}",
    };

    /// <summary>The same verdict, from a new row.</summary>
    public static string NextVerdict(ManifestRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.Status == ImageVerdict.Present
            ? $"{ImageVerdict.Present} {row.Sha256}.{row.Extension}"
            : row.Status;
    }

    private static Deviation? Compare(ManifestPair pair, string key)
    {
        var legacy = pair.Legacy.TryGetValue(key, out var path) ? LegacyVerdict(path) : null;
        var next = pair.Next.TryGetValue(key, out var row) ? NextVerdict(row) : null;
        if (legacy == next)
        {
            return null;
        }

        return new Deviation
        {
            Site = new DeviationSite
            {
                Version = pair.Version,
                Resource = $"manifest/{pair.Type}",
                Entry = key,
            },
            Kind = (legacy, next) switch
            {
                (null, _) => DeviationKind.OnlyInNext,
                (_, null) => DeviationKind.OnlyInLegacy,
                _ => DeviationKind.Value,
            },
            Legacy = legacy,
            Next = next,
            Tags = pair.Tags.GetValueOrDefault(key) ?? NoTags,
        };
    }
}
