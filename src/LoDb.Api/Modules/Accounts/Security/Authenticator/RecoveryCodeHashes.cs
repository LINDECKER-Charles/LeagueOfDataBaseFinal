using System.Security.Cryptography;
using System.Text;
using LoDb.Api.Modules.Accounts.Security.Hashing;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Modules.Accounts.Security.Authenticator;

/// <summary>
/// The recovery codes at rest: each one hashed as a password is, with argon2id, so that a
/// copy of the database gives none of them back.
/// </summary>
/// <remarks>
/// <para>
/// A code holds ten characters out of 26, about 47 bits: a fast hash of it would fall to a
/// brute force within hours. Argon2id costs a sign-in with a recovery code up to ten
/// computations, a rare event, against centuries for a brute force.
/// </para>
/// <para>
/// An entry that is no argon2 hash was written in clear by an earlier version: it still
/// matches, compared in constant time, until the next write of the list hashes it.
/// </para>
/// </remarks>
internal sealed class RecoveryCodeHashes(Argon2Passwords hashes)
{
    public static bool IsHashed(string entry) => Argon2Passwords.IsArgon2(entry);

    /// <summary>The entry to store for <paramref name="entry"/>, hashed if it is not yet.</summary>
    public string Protect(string entry) =>
        IsHashed(entry) ? entry : hashes.Hash(Encoding.UTF8.GetBytes(entry));

    /// <summary>Whether <paramref name="entry"/>, hashed or in clear, is the code.</summary>
    public bool Matches(string entry, string code)
    {
        var typed = Encoding.UTF8.GetBytes(code);
        return IsHashed(entry)
            ? hashes.Verify(entry, typed) != PasswordVerificationResult.Failed
            : CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(entry), typed);
    }
}
