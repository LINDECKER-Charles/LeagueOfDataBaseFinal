using LoDb.Infrastructure.Persistence.Accounts;

namespace LoDb.Api.Modules.Profiles.Deletion;

/// <summary>Which of the three confirmations an account's erasure asks for.</summary>
internal static class DeletionRules
{
    /// <summary>
    /// The phrase for a Google account, even one that has set a password since; the password
    /// for another account that has one; nothing otherwise.
    /// </summary>
    public static DeletionConfirmation ConfirmationOf(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.GoogleId is not null)
        {
            return DeletionConfirmation.Phrase;
        }

        return user.PasswordHash is null
            ? DeletionConfirmation.None
            : DeletionConfirmation.Password;
    }
}
