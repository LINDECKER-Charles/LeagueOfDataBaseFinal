using LoDb.Infrastructure.Analytics.Aggregation;
using LoDb.Infrastructure.Analytics.Rollup;

namespace LoDb.Infrastructure.Analytics.Import;

/// <summary>
/// Reads the legacy day files leniently (see <see cref="DailyJson"/>), then adds each day
/// that has no row, as an <c>import</c> row that no rollup replaces.
/// </summary>
internal sealed class LegacyDailyImport(DailyStore store) : ILegacyDailyImport
{
    public async Task<DailyImportReport> ImportAsync(
        string directory,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"No such directory: {directory}");
        }

        var files = LegacyDailyFile.In(directory);
        var dailies = new List<DailyAggregate>();
        foreach (var file in files)
        {
            if (await LegacyDailyFile.ReadAsync(file, cancellationToken) is { } daily)
            {
                dailies.Add(daily);
            }
        }

        var existing = await store.ExistingAsync(
            [.. dailies.Select(static daily => daily.Day)],
            cancellationToken);
        var missing = dailies.Where(daily => !existing.Contains(daily.Day)).ToList();
        var imported = dryRun ? missing.Count : await AddAsync(missing, cancellationToken);
        return new DailyImportReport
        {
            Files = files.Count,
            Imported = imported,
            AlreadyPresent = dailies.Count - imported,
            Refused = files.Count - dailies.Count,
        };
    }

    private async Task<int> AddAsync(
        IReadOnlyList<DailyAggregate> dailies,
        CancellationToken cancellationToken)
    {
        var added = 0;
        foreach (var daily in dailies)
        {
            if (await store.AddImportedAsync(daily, cancellationToken))
            {
                added++;
            }
        }

        return added;
    }
}
