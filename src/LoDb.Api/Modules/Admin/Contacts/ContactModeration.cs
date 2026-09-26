using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Admin.Http;
using LoDb.Infrastructure.Persistence;

namespace LoDb.Api.Modules.Admin.Contacts;

/// <summary>
/// Marks contact messages handled, reopens them and deletes them, each action traced with
/// the acting administrator.
/// </summary>
/// <remarks>
/// The audit vocabulary has no action for them yet: <see cref="AdminTrail"/> logs them
/// meanwhile.
/// </remarks>
internal sealed class ContactModeration(
    LoDbDbContext db,
    AdminSession session,
    AdminTrail trail,
    TimeProvider clock)
{
    public async Task<AccountProblem?> HandleAsync(int id, CancellationToken cancellationToken)
    {
        if (await db.ContactMessages.FindAsync([id], cancellationToken) is not { } message)
        {
            return AdminProblems.NotFound(AdminProblems.ContactNotFound);
        }

        message.Status = ContactStatuses.Handled;
        message.HandledAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        trail.ContactHandled(session.CurrentId, id);
        return null;
    }

    public async Task<AccountProblem?> ReopenAsync(int id, CancellationToken cancellationToken)
    {
        if (await db.ContactMessages.FindAsync([id], cancellationToken) is not { } message)
        {
            return AdminProblems.NotFound(AdminProblems.ContactNotFound);
        }

        message.Status = ContactStatuses.New;
        message.HandledAt = null;
        await db.SaveChangesAsync(cancellationToken);
        trail.ContactReopened(session.CurrentId, id);
        return null;
    }

    public async Task<AccountProblem?> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        if (await db.ContactMessages.FindAsync([id], cancellationToken) is not { } message)
        {
            return AdminProblems.NotFound(AdminProblems.ContactNotFound);
        }

        db.ContactMessages.Remove(message);
        await db.SaveChangesAsync(cancellationToken);
        trail.ContactDeleted(session.CurrentId, id);
        return null;
    }
}
