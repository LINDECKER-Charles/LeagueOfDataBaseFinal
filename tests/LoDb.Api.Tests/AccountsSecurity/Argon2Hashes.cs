using System.Security.Cryptography;
using System.Text;
using Isopoh.Cryptography.Argon2;
using Isopoh.Cryptography.SecureArray;
using LoDb.Api.Modules.Accounts.Security.Hashing;

namespace LoDb.Api.Tests.AccountsSecurity;

/// <summary>
/// Argon2 hashes made apart from the hasher under test, of any type, version or cost: those
/// of the target, changed by the test.
/// </summary>
internal static class Argon2Hashes
{
    // PHP's sizes.
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    // Zeroed but not locked in RAM, as the hasher does: a test runner may lock little memory.
    private static readonly SecureArrayCall Unlocked = new(
        SecureArray.DefaultCall.ZeroMemory,
        static (_, _) => null,
        static (_, _) => { },
        "unlocked");

    public static string Make(string password, Action<Argon2Config>? change = null)
    {
        var config = new Argon2Config
        {
            Type = Argon2Type.HybridAddressing,
            Version = Argon2Version.Nineteen,
            MemoryCost = Argon2Passwords.MemoryCostKib,
            TimeCost = Argon2Passwords.TimeCost,
            Lanes = Argon2Passwords.Lanes,
            Threads = Argon2Passwords.Lanes,
            HashLength = HashBytes,
            Salt = RandomNumberGenerator.GetBytes(SaltBytes),
            Password = Encoding.UTF8.GetBytes(password),
            SecureArrayCall = Unlocked,
        };
        change?.Invoke(config);
        using var argon2 = new Argon2(config);
        using var hash = argon2.Hash();
        return config.EncodeString(hash.Buffer);
    }
}
