using LoDb.Infrastructure.Locks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace LoDb.Infrastructure.Persistence.Baseline;

/// <summary>
/// The work of <c>migrate</c> and <c>baseline mark-applied</c>: bring a database, empty or
/// created by Doctrine, to the latest migration without replaying the legacy schema.
/// </summary>
/// <remarks>
/// <para>
/// A database that is neither empty nor exactly the Doctrine schema is refused untouched.
/// The check and the marking run in one transaction, under an advisory lock: two
/// concurrent runs never mark twice nor refuse the other's work.
/// </para>
/// <para>
/// <c>migrate</c> runs whole under a session advisory lock. EF locks its history in each
/// migration's transaction but lists the pending migrations once, so two runs with two
/// migrations pending would both apply the second.
/// </para>
/// </remarks>
public sealed partial class DatabaseMigrator(
    LoDbDbContext db,
    TimeProvider timeProvider,
    ILogger<DatabaseMigrator> logger)
{
    private const int LoggedDifferences = 20;
    private static readonly long MarkLockKey = PostgresDistributedLock.KeyOf("db:baseline");
    private static readonly long MigrateLockKey = PostgresDistributedLock.KeyOf("db:migrate");

    /// <summary>
    /// Marks the baseline if the database holds the Doctrine schema, then applies every
    /// pending migration. Idempotent; concurrent runs wait for each other.
    /// </summary>
    public async Task<MigrationReport> MigrateAsync(CancellationToken cancellationToken)
    {
        var database = db.Database;
        await database.OpenConnectionAsync(cancellationToken);
        try
        {
            await database.ExecuteSqlAsync(
                $"SELECT pg_advisory_lock({MigrateLockKey})",
                cancellationToken);
            try
            {
                return await MigrateExclusivelyAsync(cancellationToken);
            }
            finally
            {
                // Before the connection goes back to the pool, which would keep the lock.
                await database.ExecuteSqlAsync(
                    $"SELECT pg_advisory_unlock({MigrateLockKey})",
                    CancellationToken.None);
            }
        }
        finally
        {
            await database.CloseConnectionAsync();
        }
    }

    /// <summary>
    /// Records the baseline in the EF history if the database holds exactly the Doctrine
    /// schema; applies nothing. Idempotent; an empty or unexpected database is refused.
    /// </summary>
    public async Task<MigrationReport> MarkBaselineAppliedAsync(
        CancellationToken cancellationToken)
    {
        var (baseline, differences) = await EnsureBaselineAsync(
            createHistoryWhenEmpty: false,
            cancellationToken);
        switch (baseline)
        {
            case BaselineOutcome.EmptyDatabase:
                LogRefused("baseline mark-applied", "empty-database", differences);
                break;
            case BaselineOutcome.UnexpectedSchema:
                LogRefused("baseline mark-applied", "unexpected-schema", differences);
                break;
            default:
                LogBaselineChecked(logger, baseline is BaselineOutcome.Marked);
                break;
        }

        return new MigrationReport(baseline, [], differences);
    }

    private async Task<MigrationReport> MigrateExclusivelyAsync(
        CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();
        var (baseline, differences) = await EnsureBaselineAsync(
            createHistoryWhenEmpty: true,
            cancellationToken);
        if (baseline is BaselineOutcome.UnexpectedSchema)
        {
            LogRefused("migrate", "unexpected-schema", differences);
            return new MigrationReport(baseline, [], differences);
        }

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        await db.Database.MigrateAsync(cancellationToken);
        var current = db.Database.GetMigrations().Last();
        var durationMs = (long)timeProvider.GetElapsedTime(started).TotalMilliseconds;
        LogMigrated(
            logger,
            pending.Count,
            baseline is BaselineOutcome.Marked,
            current,
            durationMs);
        return new MigrationReport(baseline, pending, []);
    }

    // One line for the whole refusal: the count, then the first differences.
    private void LogRefused(string command, string reason, IReadOnlyList<string> differences)
    {
        var sample = string.Join(" | ", differences.Take(LoggedDifferences));
        LogRefused(logger, command, reason, differences.Count, sample);
    }

    private async Task<(BaselineOutcome, IReadOnlyList<string>)> EnsureBaselineAsync(
        bool createHistoryWhenEmpty,
        CancellationToken cancellationToken)
    {
        var database = db.Database;
        await using var transaction = await database.BeginTransactionAsync(cancellationToken);
        await database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock({MarkLockKey})",
            cancellationToken);

        var history = db.GetService<IHistoryRepository>();
        if (await HistoryExistsAsync(transaction, cancellationToken)
            && (await history.GetAppliedMigrationsAsync(cancellationToken))
                .Any(static row => row.MigrationId == DoctrineBaseline.MigrationId))
        {
            return (BaselineOutcome.AlreadyApplied, []);
        }

        var catalog = await SchemaCatalog.ReadAsync(
            database.GetDbConnection(),
            transaction.GetDbTransaction(),
            SchemaCatalog.Query,
            cancellationToken);
        if (catalog.Count == 0)
        {
            if (createHistoryWhenEmpty)
            {
                // EF would read the missing table first and log that failure as an error.
                await database.ExecuteSqlRawAsync(
                    history.GetCreateIfNotExistsScript(),
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            return (BaselineOutcome.EmptyDatabase, []);
        }

        IReadOnlyList<string> differences =
        [
            .. SchemaCatalog.Compare(SchemaCatalog.Doctrine, catalog),
            .. await DoctrineVersionDifferencesAsync(catalog, transaction, cancellationToken),
        ];
        if (differences.Count > 0)
        {
            return (BaselineOutcome.UnexpectedSchema, differences);
        }

        await database.ExecuteSqlRawAsync(
            history.GetCreateIfNotExistsScript(),
            cancellationToken);
        await database.ExecuteSqlRawAsync(
            history.GetInsertScript(
                new HistoryRow(DoctrineBaseline.MigrationId, ProductInfo.GetVersion())),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (BaselineOutcome.Marked, []);
    }

    // EF finds out whether its history table exists by reading it: in this transaction, a
    // failed read would abort everything after it. The catalog answers without failing.
    private async Task<bool> HistoryExistsAsync(
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "SELECT to_regclass(@name) IS NOT NULL";
        var name = command.CreateParameter();
        name.ParameterName = "name";
        name.Value = $"\"{HistoryRepository.DefaultTableName}\"";
        command.Parameters.Add(name);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    // The legacy history must list the 11 Doctrine migrations, and only them.
    private async Task<IReadOnlyList<string>> DoctrineVersionDifferencesAsync(
        IReadOnlyList<string> catalog,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (!catalog.Contains("table doctrine_migration_versions", StringComparer.Ordinal))
        {
            return [];
        }

        var versions = await SchemaCatalog.ReadAsync(
            db.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            "SELECT version FROM doctrine_migration_versions",
            cancellationToken);
        return SchemaCatalog.Compare(
            DoctrineBaseline.DoctrineVersions.Select(static v => "doctrine migration " + v),
            versions.Select(static v => "doctrine migration " + v));
    }

    [LoggerMessage(
        EventName = "db.migrate.completed",
        Level = LogLevel.Information,
        Message = "Database migrated: {Applied} migration(s) applied, baseline marked:"
            + " {BaselineMarked}, now at {Migration} ({DurationMs} ms).")]
    private static partial void LogMigrated(
        ILogger logger,
        int applied,
        bool baselineMarked,
        string migration,
        long durationMs);

    [LoggerMessage(
        EventName = "db.baseline.checked",
        Level = LogLevel.Information,
        Message = "Baseline recorded in the EF history, marked by this run: {Marked}.")]
    private static partial void LogBaselineChecked(ILogger logger, bool marked);

    [LoggerMessage(
        EventName = "db.baseline.refused",
        Level = LogLevel.Error,
        Message = "{Command} refused the database ({Reason}): {DifferenceCount} difference(s)"
            + " with the Doctrine schema. {Differences}")]
    private static partial void LogRefused(
        ILogger logger,
        string command,
        string reason,
        int differenceCount,
        string differences);
}
