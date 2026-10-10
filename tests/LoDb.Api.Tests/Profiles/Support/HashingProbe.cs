using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Tests.Profiles.Support;

/// <summary>
/// The API's password hasher, unchanged, that also counts the computations running at once
/// and keeps the most it saw since the last <see cref="Reset"/>.
/// </summary>
internal sealed class HashingProbe(IPasswordHasher<User> hasher) : IPasswordHasher<User>
{
    private int _running;
    private int _mostAtOnce;

    public int MostAtOnce => Volatile.Read(ref _mostAtOnce);

    public void Reset() => Volatile.Write(ref _mostAtOnce, 0);

    public string HashPassword(User user, string password) =>
        Measure(() => hasher.HashPassword(user, password));

    public PasswordVerificationResult VerifyHashedPassword(
        User user,
        string hashedPassword,
        string providedPassword) =>
        Measure(() => hasher.VerifyHashedPassword(user, hashedPassword, providedPassword));

    private T Measure<T>(Func<T> computation)
    {
        var running = Interlocked.Increment(ref _running);
        int most;
        do
        {
            most = Volatile.Read(ref _mostAtOnce);
        }
        while (running > most
            && Interlocked.CompareExchange(ref _mostAtOnce, running, most) != most);

        try
        {
            return computation();
        }
        finally
        {
            Interlocked.Decrement(ref _running);
        }
    }
}
