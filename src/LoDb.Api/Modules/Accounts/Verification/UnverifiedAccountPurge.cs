using System.Globalization;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Accounts.Verification;

/// <summary>
/// Erases the accounts whose e-mail is still unverified <see cref="UnverifiedPurgeOptions.After"/>
/// their creation, as their owner's own erasure does: Identity deletes the row, the database
/// removes what hangs on it, and the journal records each one as <c>account.delete</c>.
/// </summary>
/// <remarks>
/// <para>
/// An address nobody confirmed may be anyone's: the account is erased with what it holds, and
/// stops receiving e-mails. Only an account with nothing worth keeping goes. Kept whatever its
/// age: an account with a build, an API key or a donation (a payment, or what a payment
/// bought), a supporter, a banned account (whose address stays refused), an account signed
/// in through Google (whose address Google vouches for) and an account with a role.
/// </para>
/// <para>
/// An owner who verifies while the run reads the account wins: the erasure finds the row
/// changed and leaves it.
/// </para>
/// </remarks>
internal sealed class UnverifiedAccountPurge(
    LoDbDbContext db,
    UserManager<User> users,
    IAuditLog audit,
    IOptions<UnverifiedPurgeOptions> options,
    TimeProvider clock)
{
    /// <summary>The <c>reason</c> of the journal entry of each erasure.</summary>
    public const string Reason = "email-unverified";

    private const string ReasonKey = "reason";

    /// <summary>Erases a batch of such accounts.</summary>
    /// <returns>The accounts erased.</returns>
    public async Task<int> PurgeAsync(CancellationToken cancellationToken)
    {
        var erased = 0;
        foreach (var account in await ExpiredAsync(cancellationToken))
        {
            if (await TryEraseAsync(account, cancellationToken))
            {
                erased++;
            }
        }

        return erased;
    }

    private Task<List<User>> ExpiredAsync(CancellationToken cancellationToken)
    {
        var createdBefore = clock.GetUtcNow() - options.Value.After;
        return db.Users
            .Where(user => !user.EmailConfirmed && user.CreatedAt < createdBefore)
            .Where(static user => user.GoogleId == null && !user.IsBanned && !user.IsSupporter)
            .Where(user => !db.Builds.Any(build => build.OwnerId == user.Id))
            .Where(user => !db.ApiKeys.Any(key => key.UserId == user.Id))
            .Where(user => !db.Donations.Any(donation => donation.UserId == user.Id))
            .Where(user => !db.Set<IdentityUserRole<int>>().Any(role => role.UserId == user.Id))
            .OrderBy(static user => user.Id)
            .Take(options.Value.BatchSize)
            .ToListAsync(cancellationToken);
    }

    private async Task<bool> TryEraseAsync(User account, CancellationToken cancellationToken)
    {
        if (!(await users.DeleteAsync(account)).Succeeded)
        {
            // Changed since it was read, such as verified meanwhile: left for the next run.
            db.Entry(account).State = EntityState.Detached;
            return false;
        }

        await audit.RecordAsync(Erasure(account), cancellationToken);
        return true;
    }

    private static AuditEvent Erasure(User account) => new()
    {
        Action = AuditAction.AccountDelete,
        Actor = AuditActor.Anonymous,
        Target = new AuditTarget(
            AuditTargetType.User,
            account.Id.ToString(CultureInfo.InvariantCulture),
            account.UserName),
        Meta = new Dictionary<string, object?> { [ReasonKey] = Reason },
    };
}
