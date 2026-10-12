using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Ddragon;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Pipeline.Versions;

/// <summary>
/// Moves a version through <c>ddragon_version</c> (discovered, ingesting, ready, failed) and
/// promotes it.
/// </summary>
/// <remarks>
/// <para>
/// Only the holder of the version's ingestion lock writes its row, so reading and saving it
/// through the context is safe. An attempt pushes <c>next_attempt_at</c> back before it runs:
/// the date is a lease, and a version whose run died with its instance is due again once it
/// expires. The wait doubles with every attempt, six hours at most.
/// </para>
/// <para>
/// The latest version is the newest promoted one. A version is promoted when it is complete
/// and no newer version is promoted, so two instances completing two versions at once still
/// agree on the latest whatever the order of their writes.
/// </para>
/// <para>
/// Only a version behind the latest one is abandoned once its attempts are spent: the long
/// tail. A version ahead of it is the next patch, tried again every six hours at most for as
/// long as it takes. A CommunityDragon outage at the release of 16.20.1 spent its five
/// attempts in three hours, and its abandonment froze the site on the previous patch; one
/// abandoned that way before this rule is due again.
/// </para>
/// </remarks>
internal sealed class VersionStates(
    IDbContextFactory<LoDbDbContext> contexts,
    IDdragonVersionStore store,
    IOptions<IngestionOptions> options,
    TimeProvider timeProvider,
    ILogger<VersionStates> logger)
{
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromHours(6);

    /// <summary>Counts an attempt: the version turns <c>ingesting</c> unless ready.</summary>
    public async Task BeginAsync(PatchVersion version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        await store.DiscoverAsync(version.Value, cancellationToken).ConfigureAwait(false);
        await UpdateAsync(version, BeginOne, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Marks the version ready and promotes it if nothing newer is.</summary>
    /// <returns>
    /// Whether this call made the version ready, and whether it made it the latest one.
    /// </returns>
    public async Task<(bool Readied, bool Promoted)> CompleteAsync(
        PatchVersion version,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            var rows = await db.DdragonVersions
                .Where(entry => entry.Version == version.Value || entry.PromotedAt != null)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var row = rows.Single(entry => entry.Version == version.Value);
            var now = timeProvider.GetUtcNow();
            var readied = row.Status != DdragonVersionStatus.Ready;
            row.Status = DdragonVersionStatus.Ready;
            row.ReadyAt ??= now;
            row.NextAttemptAt = null;
            row.UpdatedAt = now;
            var promoted = row.PromotedAt is null && !rows.Any(other =>
                other.PromotedAt is not null && Parsed(other) > version);
            if (promoted)
            {
                row.PromotedAt = now;
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return (readied, promoted);
        }
    }

    /// <summary>
    /// Leaves the version to a later attempt, or marks it failed once its attempts are spent,
    /// unless it is ahead of the latest version. A ready version stays ready.
    /// </summary>
    public async Task DeferAsync(PatchVersion version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            var rows = await db.DdragonVersions
                .Where(entry => entry.Version == version.Value || entry.PromotedAt != null)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var row = rows.SingleOrDefault(entry => entry.Version == version.Value);
            if (row is null || row.Status == DdragonVersionStatus.Ready)
            {
                return;
            }

            var now = timeProvider.GetUtcNow();
            if (IsSpent(row, LatestOf(rows)))
            {
                Abandon(row, now);
            }
            else
            {
                Postpone(row, now);
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The versions due for an attempt, newest first. Gives up on those whose attempts are
    /// spent, and takes back those abandoned ahead of the latest version.
    /// </summary>
    public async Task<IReadOnlyList<PatchVersion>> DueAsync(CancellationToken cancellationToken)
    {
        var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            var rows = await db.DdragonVersions
                .Where(static row => row.Status != DdragonVersionStatus.Ready
                    || row.PromotedAt != null)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var latest = LatestOf(rows);
            var now = timeProvider.GetUtcNow();
            var due = rows.Where(row => IsDue(row, latest, now)).ToList();
            var spent = due.Where(row => IsSpent(row, latest)).ToList();
            foreach (var row in spent)
            {
                Abandon(row, now);
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return [.. due.Except(spent).Select(Parsed).OrderDescending()];
        }
    }

    /// <summary>The latest version: the newest promoted one, if any.</summary>
    public async Task<PatchVersion?> LatestPromotedAsync(CancellationToken cancellationToken)
    {
        var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            var promoted = await db.DdragonVersions
                .AsNoTracking()
                .Where(static row => row.PromotedAt != null)
                .Select(static row => row.Version)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            return promoted.Select(PatchVersion.Parse).Max();
        }
    }

    /// <summary>
    /// The ready versions, newest first, the latest one and when it was promoted; a promoted
    /// version stays ready.
    /// </summary>
    public async Task<(
        IReadOnlyList<PatchVersion> Ready,
        PatchVersion? Latest,
        DateTimeOffset? PromotedAt)> ReadyAsync(CancellationToken cancellationToken)
    {
        var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            var rows = await db.DdragonVersions
                .AsNoTracking()
                .Where(static row => row.Status == DdragonVersionStatus.Ready)
                .Select(static row => new { row.Version, row.PromotedAt })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var latest = rows.Where(static row => row.PromotedAt is not null)
                .MaxBy(static row => PatchVersion.Parse(row.Version));
            var ready = rows.Select(static row => PatchVersion.Parse(row.Version))
                .OrderDescending();
            return (
                [.. ready],
                latest is null ? null : PatchVersion.Parse(latest.Version),
                latest?.PromotedAt);
        }
    }

    private static PatchVersion Parsed(DdragonVersion row) => PatchVersion.Parse(row.Version);

    private async Task UpdateAsync(
        PatchVersion version,
        Action<DdragonVersion, DateTimeOffset> change,
        CancellationToken cancellationToken)
    {
        var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            var row = await db.DdragonVersions
                .SingleOrDefaultAsync(entry => entry.Version == version.Value, cancellationToken)
                .ConfigureAwait(false);
            if (row is null || row.Status == DdragonVersionStatus.Ready)
            {
                return;
            }

            change(row, timeProvider.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void BeginOne(DdragonVersion row, DateTimeOffset now)
    {
        row.Status = DdragonVersionStatus.Ingesting;
        row.Attempts++;
        row.NextAttemptAt = now + Backoff(row.Attempts);
        row.UpdatedAt = now;
    }

    // Waiting for an attempt: unleased, or once its lease or its wait has run out. An
    // abandoned version ahead of the latest one was abandoned before such versions were
    // spared, and is taken back at once.
    private static bool IsDue(DdragonVersion row, PatchVersion? latest, DateTimeOffset now) =>
        row.Status switch
        {
            DdragonVersionStatus.Discovered or DdragonVersionStatus.Ingesting =>
                row.NextAttemptAt is null || row.NextAttemptAt <= now,
            DdragonVersionStatus.Failed => IsAhead(row, latest),
            _ => false,
        };

    private bool IsSpent(DdragonVersion row, PatchVersion? latest) =>
        row.Attempts >= options.Value.MaxAttempts && !IsAhead(row, latest);

    // Newer than every promoted version, or nothing is promoted yet: the next patch.
    private static bool IsAhead(DdragonVersion row, PatchVersion? latest) =>
        Parsed(row) > latest;

    private static PatchVersion? LatestOf(IEnumerable<DdragonVersion> rows) =>
        rows.Where(static row => row.PromotedAt is not null).Select(Parsed).Max();

    private void Postpone(DdragonVersion row, DateTimeOffset now)
    {
        row.Status = DdragonVersionStatus.Discovered;
        row.NextAttemptAt = now + Backoff(row.Attempts);
        row.UpdatedAt = now;
    }

    private void Abandon(DdragonVersion row, DateTimeOffset now)
    {
        row.Status = DdragonVersionStatus.Failed;
        row.NextAttemptAt = null;
        row.UpdatedAt = now;
        IngestionLog.VersionAbandoned(logger, row.Version, row.Attempts);
    }

    private TimeSpan Backoff(int attempts)
    {
        var delay = options.Value.RetryDelay;
        for (var attempt = 1; attempt < attempts && delay < MaxRetryDelay; attempt++)
        {
            delay *= 2;
        }

        return delay < MaxRetryDelay ? delay : MaxRetryDelay;
    }
}
