using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Accounts.Security;

/// <summary>
/// Identity's user manager, which also counts the lockouts in <see cref="SecurityMetrics"/>.
/// </summary>
/// <remarks>
/// Every failure that counts toward the lockout comes through <see cref="AccessFailedAsync"/>,
/// whatever asked for the password or the code: a sign-in, a second factor, an erasure, the
/// enrolment of an authenticator. Counting here sees them all.
/// </remarks>
internal sealed class LoDbUserManager(
    IUserStore<User> store,
    IOptions<IdentityOptions> optionsAccessor,
    IPasswordHasher<User> passwordHasher,
    IEnumerable<IUserValidator<User>> userValidators,
    IEnumerable<IPasswordValidator<User>> passwordValidators,
    ILookupNormalizer keyNormalizer,
    IdentityErrorDescriber errors,
    IServiceProvider services,
    ILogger<UserManager<User>> logger,
    SecurityMetrics metrics)
    : UserManager<User>(
        store,
        optionsAccessor,
        passwordHasher,
        userValidators,
        passwordValidators,
        keyNormalizer,
        errors,
        services,
        logger)
{
    public override async Task<IdentityResult> AccessFailedAsync(User user)
    {
        var wasLocked = await IsLockedOutAsync(user);
        var result = await base.AccessFailedAsync(user);
        if (result.Succeeded && !wasLocked && await IsLockedOutAsync(user))
        {
            metrics.RecordLockout(await IsInRoleAsync(user, Role.Admin));
        }

        return result;
    }
}
