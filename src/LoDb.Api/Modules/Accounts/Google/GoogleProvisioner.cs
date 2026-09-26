using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Accounts.Registration;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Accounts.Google;

/// <summary>
/// Maps a Google identity to an account, as the legacy <c>GoogleAccountProvisioner</c> does:
/// by Google id; else the account of the same e-mail, only when Google vouches for it; else
/// a new account, without a password.
/// </summary>
/// <remarks>
/// Two changes from the legacy stack: a new account is verified only when Google vouches for
/// its e-mail, and tying Google to an account whose e-mail was never verified removes its
/// password, which whoever registered the address without owning it may have chosen.
/// </remarks>
internal sealed class GoogleProvisioner(
    UserManager<User> users,
    LoDbDbContext db,
    TimeProvider clock)
{
    // Length of users.google_id; Google's ids have 21 digits.
    private const int MaxSubjectLength = 30;

    // Tries left when concurrent sign-ups take the username allocated to a new account.
    private const int MaxCreateAttempts = 3;

    private const char EmailMark = '@';

    public async Task<GoogleProvisioning> ProvisionAsync(
        GoogleProfile profile,
        CancellationToken cancellationToken)
    {
        if (profile.Subject.Length > MaxSubjectLength
            || profile.Email.Length > RegistrationRules.MaxEmailLength)
        {
            return Unlinkable();
        }

        var known = await db.Users.FirstOrDefaultAsync(
            user => user.GoogleId == profile.Subject,
            cancellationToken);
        if (known is not null)
        {
            return await VouchAsync(known, profile);
        }

        return await users.FindByEmailAsync(profile.Email) switch
        {
            null => await CreateAsync(profile, cancellationToken),
            var owner when profile.EmailVerified => await LinkAsync(owner, profile),
            _ => GoogleProvisioning.Failed(GoogleFailures.EmailUnverified),
        };
    }

    // A later sign-in may bring the vouching the first one lacked.
    private async Task<GoogleProvisioning> VouchAsync(User known, GoogleProfile profile)
    {
        if (known.EmailConfirmed
            || !profile.EmailVerified
            || !string.Equals(known.Email, profile.Email, StringComparison.OrdinalIgnoreCase))
        {
            return GoogleProvisioning.Of(known);
        }

        known.EmailConfirmed = true;
        return await SaveAsync(known);
    }

    private async Task<GoogleProvisioning> LinkAsync(User owner, GoogleProfile profile)
    {
        // Removing the password also changes the stamp: the squatter's sessions close.
        if (!owner.EmailConfirmed
            && owner.PasswordHash is not null
            && !(await users.RemovePasswordAsync(owner)).Succeeded)
        {
            return Unlinkable();
        }

        owner.GoogleId = profile.Subject;
        owner.EmailConfirmed = true;
        return await SaveAsync(owner);
    }

    private async Task<GoogleProvisioning> SaveAsync(User user)
    {
        try
        {
            return (await users.UpdateAsync(user)).Succeeded
                ? GoogleProvisioning.Of(user)
                : Unlinkable();
        }
        catch (DbUpdateException)
        {
            // Another sign-in tied this Google id to an account in the meantime.
            return Unlinkable();
        }
    }

    private async Task<GoogleProvisioning> CreateAsync(
        GoogleProfile profile,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxCreateAttempts; attempt++)
        {
            if (await TryCreateAsync(profile, cancellationToken) is { } provisioned)
            {
                return provisioned;
            }
        }

        return Unlinkable();
    }

    // Null when another account took the allocated username since.
    private async Task<GoogleProvisioning?> TryCreateAsync(
        GoogleProfile profile,
        CancellationToken cancellationToken)
    {
        var user = await NewUserAsync(profile, cancellationToken);
        try
        {
            var created = await users.CreateAsync(user);
            if (created.Succeeded)
            {
                return GoogleProvisioning.Of(user);
            }

            return created.Errors.Any(static error =>
                error.Code == nameof(IdentityErrorDescriber.DuplicateUserName))
                ? null
                : Unlinkable();
        }
        catch (DbUpdateException exception)
        {
            db.Entry(user).State = EntityState.Detached;
            return UniqueViolations.FieldOf(exception) == AccountFields.Username
                ? null
                : Unlinkable();
        }
    }

    private async Task<User> NewUserAsync(GoogleProfile profile, CancellationToken token)
    {
        var localPart = profile.Email.Split(EmailMark, 2)[0];
        var username = await UsernameAllocator.AllocateAsync(
            [profile.GivenName, localPart],
            IsTakenAsync,
            token);
        return new User
        {
            UserName = username,
            Email = profile.Email,
            GoogleId = profile.Subject,
            EmailConfirmed = profile.EmailVerified,
            Roles = [],
            CreatedAt = clock.GetUtcNow(),
        };
    }

    private Task<bool> IsTakenAsync(string username, CancellationToken cancellationToken)
    {
        var normalized = users.NormalizeName(username);
        return db.Users.AnyAsync(
            user => user.NormalizedUserName == normalized,
            cancellationToken);
    }

    private static GoogleProvisioning Unlinkable() =>
        GoogleProvisioning.Failed(GoogleFailures.Failed);
}
