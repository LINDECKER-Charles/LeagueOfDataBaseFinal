using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Modules.Accounts.SignIn;

/// <summary>
/// Finds the account a sign-in names: by e-mail when the identifier has an <c>@</c>, which
/// no username has, by username otherwise, whatever the case.
/// </summary>
internal sealed class AccountLookup(UserManager<User> users)
{
    // As long as the longest e-mail an account can have: anything longer names no account.
    private const int MaxIdentifierLength = 180;

    private const char EmailMark = '@';

    public async Task<User?> FindAsync(string identifier)
    {
        var trimmed = identifier.Trim();
        if (trimmed.Length is 0 or > MaxIdentifierLength)
        {
            return null;
        }

        return trimmed.Contains(EmailMark, StringComparison.Ordinal)
            ? await users.FindByEmailAsync(trimmed)
            : await users.FindByNameAsync(trimmed);
    }
}
