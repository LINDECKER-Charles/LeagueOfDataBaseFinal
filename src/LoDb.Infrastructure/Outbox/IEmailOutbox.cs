namespace LoDb.Infrastructure.Outbox;

/// <summary>
/// Queues e-mails in <c>email_outbox</c>, in the same transaction as the change that sends
/// them; a background worker delivers them.
/// </summary>
public interface IEmailOutbox
{
    /// <summary>
    /// Adds <paramref name="message"/> to the scoped <c>LoDbDbContext</c>: it is written by
    /// the caller's next <c>SaveChangesAsync</c>, or not at all if that change fails.
    /// </summary>
    Task EnqueueAsync(EmailMessage message, CancellationToken cancellationToken);
}
