using System.Globalization;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Admin.Http;
using LoDb.Api.Modules.PublicApi;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Admin.Users;

/// <summary>
/// Bans, unbans and deletes accounts, each action recorded in the audit journal.
/// </summary>
/// <remarks>
/// A ban renews the security stamp: the sessions and refresh tokens of the account close at
/// their next check, and the sign-in manager refuses the account meanwhile. A deletion lets
/// the database remove what hangs on the account (builds, votes, API keys) and tells the
/// public API at once that the keys are gone.
/// </remarks>
internal sealed class UserModeration(
    UserManager<User> users,
    LoDbDbContext db,
    IApiKeyCache keys,
    IAuditLog audit,
    AdminSession session,
    TimeProvider clock)
{
    public const string ReasonField = "reason";
    public const string ReasonTooLong = "too-long";

    private const int ReasonLength = 255;
    private const string ReasonKey = "reason";

    public async Task<AccountProblem?> BanAsync(
        int id,
        string? reason,
        CancellationToken cancellationToken)
    {
        var trimmed = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (trimmed?.Length > ReasonLength)
        {
            return AdminProblems.Invalid(ReasonField, ReasonTooLong);
        }

        if (await ModeratedAsync(id) is not { } user)
        {
            return Refusal(id);
        }

        user.IsBanned = true;
        user.BannedAt = clock.GetUtcNow();
        user.BanReason = trimmed;
        await Succeed(users.UpdateSecurityStampAsync(user), id);
        var line = Line(AuditAction.AdminUserBan, user) with
        {
            Meta = new Dictionary<string, object?> { [ReasonKey] = trimmed },
        };
        await audit.RecordAsync(line, cancellationToken);
        return null;
    }

    public async Task<AccountProblem?> UnbanAsync(int id, CancellationToken cancellationToken)
    {
        if (await FindAsync(id) is not { } user)
        {
            return AdminProblems.NotFound(AdminProblems.UserNotFound);
        }

        user.IsBanned = false;
        user.BannedAt = null;
        user.BanReason = null;
        await Succeed(users.UpdateAsync(user), id);
        await audit.RecordAsync(Line(AuditAction.AdminUserUnban, user), cancellationToken);
        return null;
    }

    public async Task<AccountProblem?> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        if (await ModeratedAsync(id) is not { } user)
        {
            return Refusal(id);
        }

        var owned = await db.ApiKeys.AsNoTracking()
            .Where(key => key.UserId == id)
            .ToListAsync(cancellationToken);
        await Succeed(users.DeleteAsync(user), id);

        // After the commit: a reload before it would find the keys still there.
        foreach (var key in owned)
        {
            keys.Invalidate(key);
        }

        await audit.RecordAsync(Line(AuditAction.AdminUserDelete, user), cancellationToken);
        return null;
    }

    private Task<User?> FindAsync(int id) =>
        users.FindByIdAsync(id.ToString(CultureInfo.InvariantCulture));

    // An administrator never bans or deletes their own account: nobody could undo it.
    private async Task<User?> ModeratedAsync(int id) =>
        id == session.CurrentId ? null : await FindAsync(id);

    private AccountProblem Refusal(int id) =>
        id == session.CurrentId
            ? AdminProblems.Conflict(AdminProblems.SelfModeration)
            : AdminProblems.NotFound(AdminProblems.UserNotFound);

    private static async Task Succeed(Task<IdentityResult> change, int id)
    {
        var result = await change;
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Account {id} could not be changed: {result.Errors.First().Code}.");
        }
    }

    private static AuditEvent Line(AuditAction action, User user) =>
        new() { Action = action, Target = AdminTargets.Of(user) };
}
