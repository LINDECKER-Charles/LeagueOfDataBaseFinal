using LoDb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Audit.Retention;

/// <summary>
/// The six months the journal keeps (the CNIL baseline for security logs), and the deletion
/// of what is older.
/// </summary>
/// <remarks>
/// Whole UTC days, as in the legacy stack: the day six months before today is kept, every
/// earlier one goes. <see cref="Months"/> is the single place to change the period.
/// </remarks>
internal sealed class AuditRetention(LoDbDbContext db, TimeProvider clock)
{
    public const int Months = 6;

    /// <summary>The first instant kept now: midnight UTC, six months before today.</summary>
    public DateTimeOffset Cutoff => CutoffAt(clock.GetUtcNow());

    /// <summary>The first instant kept at <paramref name="now"/>.</summary>
    public static DateTimeOffset CutoffAt(DateTimeOffset now) =>
        new(now.UtcDateTime.Date.AddMonths(-Months), TimeSpan.Zero);

    /// <summary>Deletes the entries older than <see cref="Cutoff"/>.</summary>
    /// <returns>The number of entries deleted.</returns>
    public Task<int> DeleteExpiredAsync(CancellationToken cancellationToken) =>
        DeleteBeforeAsync(Cutoff, cancellationToken);

    /// <summary>
    /// Deletes the entries recorded before <paramref name="before"/>, or all of them when it
    /// is null.
    /// </summary>
    /// <returns>The number of entries deleted.</returns>
    public Task<int> DeleteBeforeAsync(DateTimeOffset? before, CancellationToken cancellationToken)
    {
        var entries = before is { } instant
            ? db.AuditLog.Where(entry => entry.OccurredAt < instant)
            : db.AuditLog;
        return entries.ExecuteDeleteAsync(cancellationToken);
    }
}
