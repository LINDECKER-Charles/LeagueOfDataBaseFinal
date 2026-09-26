using System.Globalization;
using LoDb.Api.Modules.Audit.Reading.Query;
using LoDb.Api.Modules.Audit.Reading.Views;
using LoDb.Api.Modules.Audit.Retention;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Audit;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Audit.Reading;

/// <summary>
/// The read side of the journal: the whole feed, the activity of one account, its footprint.
/// </summary>
/// <remarks>
/// Newest first, the id breaking ties within an instant, over the indexes of
/// <c>audit_log</c> on the time, the actor and the action.
/// </remarks>
internal sealed class AuditReader(LoDbDbContext db)
{
    /// <summary>The entries <paramref name="query"/> keeps.</summary>
    public Task<AuditPage> JournalAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return PageAsync(query.Filter.Apply(db.AuditLog), query.Window, cancellationToken);
    }

    /// <summary>
    /// What <paramref name="subject"/> did or underwent: the entries it acted in, those
    /// targeting it by id, and, as in the legacy journal, those naming it by its e-mail or
    /// username (a failed sign-in, a registration).
    /// </summary>
    public Task<AuditPage> ActivityAsync(
        AuditSubject subject,
        AuditQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(query);
        var id = subject.Id;
        var idText = id.ToString(CultureInfo.InvariantCulture);
        // Case-insensitive exact matches of the labels the account still has.
        var username = subject.Username is { } name ? LikeEscape.Escape(name) : null;
        var email = subject.Email is { } address ? LikeEscape.Escape(address) : null;
        var entries = db.AuditLog.Where(entry =>
            entry.ActorId == id
            || (entry.TargetType == AuditTargetType.User && entry.TargetId == idText)
            || (entry.Target != null
                && ((username != null
                        && EF.Functions.ILike(entry.Target, username, LikeEscape.Character))
                    || (email != null
                        && EF.Functions.ILike(entry.Target, email, LikeEscape.Character)))));
        return PageAsync(query.Filter.Apply(entries), query.Window, cancellationToken);
    }

    /// <summary>The account <paramref name="userId"/> names, or null once deleted.</summary>
    public Task<AuditSubject?> SubjectAsync(int userId, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new AuditSubject(user.Id, user.UserName, user.Email))
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>How much the journal holds, for the purge screen.</summary>
    public async Task<AuditVolume> VolumeAsync(
        DateTimeOffset retentionCutoff,
        CancellationToken cancellationToken)
    {
        var span = await db.AuditLog
            .GroupBy(static _ => 1)
            .Select(static entries => new
            {
                Count = entries.LongCount(),
                Oldest = entries.Min(entry => (DateTimeOffset?)entry.OccurredAt),
                Newest = entries.Max(entry => (DateTimeOffset?)entry.OccurredAt),
            })
            .SingleOrDefaultAsync(cancellationToken);
        var bytes = await db.Database
            .SqlQueryRaw<long>("SELECT pg_total_relation_size('audit_log') AS \"Value\"")
            .SingleAsync(cancellationToken);
        return new AuditVolume
        {
            Entries = span?.Count ?? 0,
            Oldest = span?.Oldest,
            Newest = span?.Newest,
            TotalBytes = bytes,
            RetentionMonths = AuditRetention.Months,
            RetentionCutoff = retentionCutoff,
        };
    }

    private static async Task<AuditPage> PageAsync(
        IQueryable<AuditLogEntry> entries,
        PageWindow window,
        CancellationToken cancellationToken)
    {
        // One row past the page proves that another follows.
        var rows = await entries.AsNoTracking()
            .OrderByDescending(static entry => entry.OccurredAt)
            .ThenByDescending(static entry => entry.Id)
            .Skip(window.Offset)
            .Take(window.PageSize + 1)
            .ToListAsync(cancellationToken);
        return new AuditPage
        {
            Items = [.. rows.Take(window.PageSize).Select(AuditEntryView.Of)],
            Page = window.Page,
            PageSize = window.PageSize,
            HasMore = rows.Count > window.PageSize,
        };
    }
}
