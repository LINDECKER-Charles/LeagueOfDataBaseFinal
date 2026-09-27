using LoDb.Api.Modules.Admin.Storage.Views;
using LoDb.Infrastructure.Storage;
using LoDb.Infrastructure.Storage.Blobs;

namespace LoDb.Api.Modules.Admin.Storage;

/// <summary>
/// One walk of the storage root, sorting each object by the layout of ADR 0004:
/// <c>blobs/{sha256}.{ext}</c>, <c>data/{version}/{lang}/{type}.json</c>, anything else.
/// </summary>
/// <remarks>
/// The staging area and the hidden files of the root (the probes of the readiness check)
/// are not stored objects and are skipped.
/// </remarks>
internal sealed class StorageScan
{
    public const string OtherFamily = "other";

    private const char Separator = '/';
    private const char Hidden = '.';
    private const int BlobDepth = 2;
    private const int DatasetDepth = 4;
    private const int LargestCount = 15;

    private readonly Dictionary<string, long> _pngs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _webps = new(StringComparer.Ordinal);
    private readonly SortedDictionary<DateOnly, (long Objects, long Bytes)> _days = [];
    private readonly List<StoredObject> _objects = [];

    public StorageTally Families { get; } = new();

    public StorageTally Extensions { get; } = new();

    public StorageTally Versions { get; } = new();

    public StorageTally Langs { get; } = new();

    public StorageTally Types { get; } = new();

    public DatasetCoverage Coverage { get; } = new();

    /// <summary>Walks <paramref name="root"/>; throws when it cannot be read.</summary>
    public static StorageScan Of(DirectoryInfo root, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        var scan = new StorageScan();
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = 0,
        };
        foreach (var file in root.EnumerateFiles("*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = Path.GetRelativePath(root.FullName, file.FullName)
                .Replace(Path.DirectorySeparatorChar, Separator);
            if (path[0] != Hidden)
            {
                scan.Add(path, file.Length, file.LastWriteTimeUtc);
            }
        }

        return scan;
    }

    /// <summary>PNG blobs and their bytes, by SHA-256.</summary>
    public IReadOnlyDictionary<string, long> Pngs => _pngs;

    /// <summary>WebP blobs and their bytes, by SHA-256.</summary>
    public IReadOnlyDictionary<string, long> Webps => _webps;

    public IReadOnlyList<StoredObject> Largest() =>
        [.. _objects.OrderByDescending(static item => item.Bytes).Take(LargestCount)];

    public IReadOnlyList<StorageDay> Timeline()
    {
        var cumulative = 0L;
        var days = new List<StorageDay>(_days.Count);
        foreach (var (date, (objects, bytes)) in _days)
        {
            cumulative += bytes;
            days.Add(new StorageDay
            {
                Date = date,
                Objects = objects,
                Bytes = bytes,
                CumulativeBytes = cumulative,
            });
        }

        return days;
    }

    private void Add(string path, long bytes, DateTime written)
    {
        var segments = path.Split(Separator);
        var family = (segments[0], segments.Length) switch
        {
            (StorageLayout.BlobsDirectory, BlobDepth) => AddBlob(segments[1], bytes),
            (StorageLayout.DataDirectory, DatasetDepth) => AddDataset(segments, bytes),
            _ => OtherFamily,
        };
        Families.Add(family, bytes);
        var day = DateOnly.FromDateTime(written);
        var (objects, total) = _days.GetValueOrDefault(day);
        _days[day] = (objects + 1, total + bytes);
        _objects.Add(new StoredObject { Path = path, Bytes = bytes });
    }

    private string AddBlob(string fileName, long bytes)
    {
        var extension = Path.GetExtension(fileName).TrimStart(Hidden);
        var sha = Path.GetFileNameWithoutExtension(fileName);
        Extensions.Add(extension, bytes);
        if (extension == WebpSibling.SourceExtension)
        {
            _pngs[sha] = bytes;
        }
        else if (extension == WebpSibling.Extension)
        {
            _webps[sha] = bytes;
        }

        return StorageLayout.BlobsDirectory;
    }

    private string AddDataset(string[] segments, long bytes)
    {
        var (version, lang) = (segments[1], segments[2]);
        var type = Path.GetFileNameWithoutExtension(segments[3]);
        Versions.Add(version, bytes);
        Langs.Add(lang, bytes);
        Types.Add(type, bytes);
        Coverage.Add(version, lang, type);
        return StorageLayout.DataDirectory;
    }
}
