using LoDb.Api.Modules.Admin.Backups.Views;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Backups;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Backups;

/// <summary>
/// The journal of the backups, read-only: the backup container of infra-vps writes it, the
/// API never does.
/// </summary>
internal sealed class BackupJournal(
    LoDbDbContext db,
    TimeProvider clock,
    IOptions<BackupOptions> options)
{
    // Far past what a window holds, even nights of hourly retries: a bound, not a page.
    private const int MaxRuns = 1000;
    private const decimal BytesPerGb = 1_000_000_000m;
    private const int CostDecimals = 4;

    /// <summary>The report, with the steps of the last <paramref name="days"/> days.</summary>
    public async Task<BackupReport> ReportAsync(int days, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return new BackupReport
        {
            GeneratedAt = now,
            Days = days,
            Databases = BackupDatabaseStates.Of(await LatestAsync(cancellationToken), now),
            Repository = await RepositoryAsync(cancellationToken),
            Runs = await RunsAsync(now.AddDays(-days), cancellationToken),
        };
    }

    // The newest row of each database, step and outcome, whatever its age.
    private async Task<List<BackupRun>> LatestAsync(CancellationToken cancellationToken) =>
        await db.BackupRuns.AsNoTracking()
            .Where(static run => run.Database != null)
            .GroupBy(static run => new { run.Database, run.Step, run.Succeeded })
            .Select(static outcome =>
                outcome.OrderByDescending(static run => run.FinishedAt).First())
            .ToListAsync(cancellationToken);

    private async Task<BackupRepositoryState?> RepositoryAsync(CancellationToken cancellationToken)
    {
        var measure = await db.BackupRuns.AsNoTracking()
            .Where(static run => run.Step == BackupStep.Retention
                && run.Succeeded
                && run.RepositoryBytes != null)
            .OrderByDescending(static run => run.FinishedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (measure?.RepositoryBytes is not { } bytes)
        {
            return null;
        }

        var price = options.Value.PricePerGbMonth;
        return new BackupRepositoryState
        {
            Bytes = bytes,
            MeasuredAt = measure.FinishedAt,
            PricePerGbMonth = price,
            Currency = options.Value.Currency,
            MonthlyCost = Math.Round(bytes / BytesPerGb * price, CostDecimals),
        };
    }

    private async Task<IReadOnlyList<BackupRunRow>> RunsAsync(
        DateTimeOffset since,
        CancellationToken cancellationToken)
    {
        var runs = await db.BackupRuns.AsNoTracking()
            .Where(run => run.StartedAt >= since)
            .OrderByDescending(static run => run.StartedAt)
            .ThenByDescending(static run => run.Id)
            .Take(MaxRuns)
            .ToListAsync(cancellationToken);
        return [.. runs.Select(static run => new BackupRunRow
        {
            Id = run.Id,
            Database = run.Database,
            Step = BackupColumns.ToText(run.Step),
            StartedAt = run.StartedAt,
            FinishedAt = run.FinishedAt,
            Succeeded = run.Succeeded,
            BytesRead = run.BytesRead,
            BytesAdded = run.BytesAdded,
            RepositoryBytes = run.RepositoryBytes,
            Detail = run.Detail,
        })];
    }
}
