using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Modules.Accounts.Security.Hashing;

/// <summary>
/// Reads every hash the legacy stack writes and writes argon2id that PHP's
/// <c>password_verify</c> reads, so that a rollback signs nobody out.
/// </summary>
/// <remarks>
/// bcrypt, argon2i and argon2id weaker than the target answer
/// <see cref="PasswordVerificationResult.SuccessRehashNeeded"/>: <c>UserManager</c> then
/// rewrites the hash at the sign-in. Symfony's rules hold: an empty password or one over
/// 4096 bytes never matches, and an unknown format fails.
/// </remarks>
internal sealed class MigratingPasswordHasher(Argon2Passwords argon2) : IPasswordHasher<User>
{
    public string HashPassword(User user, string password)
    {
        var bytes = PasswordInput.Bytes(password) ?? throw new ArgumentException(
            $"A password is 1 to {PasswordInput.MaxBytes} bytes of UTF-8.",
            nameof(password));
        return argon2.Hash(bytes);
    }

    public PasswordVerificationResult VerifyHashedPassword(
        User user,
        string hashedPassword,
        string providedPassword)
    {
        var password = PasswordInput.Bytes(providedPassword);
        if (password is null)
        {
            return PasswordVerificationResult.Failed;
        }

        if (LegacyBcrypt.IsBcrypt(hashedPassword))
        {
            return LegacyBcrypt.Verify(hashedPassword, providedPassword)
                ? PasswordVerificationResult.SuccessRehashNeeded
                : PasswordVerificationResult.Failed;
        }

        return Argon2Passwords.IsArgon2(hashedPassword)
            ? argon2.Verify(hashedPassword, password)
            : PasswordVerificationResult.Failed;
    }
}
