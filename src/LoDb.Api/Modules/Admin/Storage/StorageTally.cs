using LoDb.Api.Modules.Admin.Storage.Views;

namespace LoDb.Api.Modules.Admin.Storage;

/// <summary>Objects and bytes counted by name: an extension, a version, a family.</summary>
internal sealed class StorageTally
{
    private const double Percent = 100;

    private readonly Dictionary<string, (long Objects, long Bytes)> _counts =
        new(StringComparer.Ordinal);

    public long Objects => _counts.Values.Sum(static count => count.Objects);

    public long Bytes => _counts.Values.Sum(static count => count.Bytes);

    public void Add(string name, long bytes)
    {
        var (objects, total) = _counts.GetValueOrDefault(name);
        _counts[name] = (objects + 1, total + bytes);
    }

    /// <summary>The names, the heaviest first, each with its share of the bytes.</summary>
    public IReadOnlyList<StorageRow> Rows()
    {
        var bytes = Bytes;
        return
        [
            .. _counts
                .OrderByDescending(static count => count.Value.Bytes)
                .ThenBy(static count => count.Key, StringComparer.Ordinal)
                .Select(count => new StorageRow
                {
                    Name = count.Key,
                    Objects = count.Value.Objects,
                    Bytes = count.Value.Bytes,
                    Pct = bytes > 0 ? count.Value.Bytes * Percent / bytes : 0,
                }),
        ];
    }
}
