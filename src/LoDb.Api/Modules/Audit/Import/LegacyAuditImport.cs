using LoDb.Api.Modules.Audit.Retention;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Audit;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Audit.Import;

/// <summary>
/// Copies the last six months of the legacy journal into <c>audit_log</c>, so that an
/// investigation loses nothing at the switch-over.
/// </summary>
/// <remarks>
/// Day by day, oldest first, each day in one transaction. An entry already in the journal
/// with the same <see cref="ImportKey"/> is not added again, so an interrupted import is
/// simply run again; two identical lines of one day stay two entries.
/// </remarks>
internal sealed class LegacyAuditImport(LoDbDbContext db, TimeProvider clock)
{
    /// <param name="directories">The directories of day files, the local copy first.</param>
    /// <param name="dryRun">Reads and counts, writes nothing.</param>
    /// <param name="cancellationToken">Stops between two days.</param>
    /// <exception cref="DirectoryNotFoundException">A directory does not exist.</exception>
    public async Task<AuditImportReport> ImportAsync(
        IReadOnlyList<string> directories,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var cutoff = AuditRetention.CutoffAt(clock.GetUtcNow());
        var firstDay = DateOnly.FromDateTime(cutoff.UtcDateTime);
        var report = new AuditImportReport();
        foreach (var (day, file) in LegacyJournalDays.Index(directories))
        {
            report = report.Add(day < firstDay
                ? new AuditImportReport { ExpiredDays = 1 }
                : await ImportDayAsync(file, new DayRun(cutoff, dryRun), cancellationToken));
        }

        return report;
    }

    private async Task<AuditImportReport> ImportDayAsync(
        string file,
        DayRun run,
        CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync(file, cancellationToken);
        var parsed = lines.Where(static line => !string.IsNullOrWhiteSpace(line))
            .Select(LegacyAuditLine.Parse)
            .ToList();
        List<AuditLogEntry> entries =
            [.. parsed.OfType<AuditLogEntry>().Where(entry => entry.OccurredAt >= run.Cutoff)];
        var fresh = await WithoutRecordedAsync(entries, cancellationToken);
        if (!run.DryRun && fresh.Count > 0)
        {
            db.AuditLog.AddRange(fresh);
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }

        return new AuditImportReport
        {
            Days = 1,
            Imported = fresh.Count,
            AlreadyPresent = entries.Count - fresh.Count,
            Refused = parsed.Count(static entry => entry is null),
        };
    }

    // Each entry already recorded cancels one read entry of the same key.
    private async Task<List<AuditLogEntry>> WithoutRecordedAsync(
        List<AuditLogEntry> entries,
        CancellationToken cancellationToken)
    {
        if (entries.Count == 0)
        {
            return entries;
        }

        var first = entries.Min(static entry => entry.OccurredAt);
        var last = entries.Max(static entry => entry.OccurredAt);
        var recorded = (await db.AuditLog.AsNoTracking()
                .Where(entry => entry.OccurredAt >= first && entry.OccurredAt <= last)
                .ToListAsync(cancellationToken))
            .Select(ImportKey.Of)
            .CountBy(static key => key)
            .ToDictionary();
        return [.. entries.Where(entry => !Consume(recorded, ImportKey.Of(entry)))];
    }

    private static bool Consume(Dictionary<ImportKey, int> recorded, ImportKey key)
    {
        if (!recorded.TryGetValue(key, out var count) || count == 0)
        {
            return false;
        }

        recorded[key] = count - 1;
        return true;
    }

    // What every day of one run shares: the first instant kept, and whether it writes.
    private sealed record DayRun(DateTimeOffset Cutoff, bool DryRun);
}
