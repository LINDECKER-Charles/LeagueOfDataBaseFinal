using System.Security.Cryptography;
using Isopoh.Cryptography.Argon2;
using Isopoh.Cryptography.SecureArray;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Modules.Accounts.Security.Hashing;

/// <summary>
/// Argon2 hashes in the PHC format of PHP's <c>password_hash</c>: written as argon2id, read
/// as argon2id or argon2i.
/// </summary>
/// <remarks>
/// The target (19 MiB, 2 passes, 1 lane) is OWASP's minimum for argon2id, measured at 42 ms
/// and 20 MiB per hash, against 206 ms and 67 MiB at PHP's defaults (64 MiB, 4 passes),
/// which four sign-ins at once would push past the API's memory limit.
/// </remarks>
internal sealed class Argon2Passwords(HashingGate gate)
{
    /// <summary>Memory cost of new hashes, in KiB.</summary>
    public const int MemoryCostKib = 19_456;

    /// <summary>Passes over the memory of new hashes.</summary>
    public const int TimeCost = 2;

    /// <summary>One lane: libsodium, which the legacy stack verifies with, needs it.</summary>
    public const int Lanes = 1;

    // PHP's sizes.
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    // The most any hash of the site was made with (PHP's default): a stored hash asking for
    // more is refused rather than allowed to take the memory of the API.
    private const int MaxMemoryCostKib = 65_536;

    private static readonly string[] Prefixes = ["$argon2id$", "$argon2i$"];

    // Buffers are zeroed but not locked in RAM: locking 19 MiB exceeds what a container may
    // lock, and every hash would first fail to.
    private static readonly SecureArrayCall Unlocked = new(
        SecureArray.DefaultCall.ZeroMemory,
        static (_, _) => null,
        static (_, _) => { },
        "unlocked");

    public static bool IsArgon2(string hash) =>
        Prefixes.Any(prefix => hash.StartsWith(prefix, StringComparison.Ordinal));

    public string Hash(byte[] password)
    {
        var config = new Argon2Config
        {
            Type = Argon2Type.HybridAddressing,
            Version = Argon2Version.Nineteen,
            MemoryCost = MemoryCostKib,
            TimeCost = TimeCost,
            Lanes = Lanes,
            Threads = Lanes,
            HashLength = HashBytes,
            Salt = RandomNumberGenerator.GetBytes(SaltBytes),
            Password = password,
            SecureArrayCall = Unlocked,
        };
        return gate.Run(() =>
        {
            using var argon2 = new Argon2(config);
            using var hash = argon2.Hash();
            return config.EncodeString(hash.Buffer);
        });
    }

    public PasswordVerificationResult Verify(string encoded, byte[] password)
    {
        var config = new Argon2Config { Password = password, SecureArrayCall = Unlocked };
        if (!TryDecode(config, encoded, out var expected))
        {
            return PasswordVerificationResult.Failed;
        }

        using (expected)
        {
            if (config.MemoryCost > MaxMemoryCostKib || !gate.Run(() => Matches(config, expected)))
            {
                return PasswordVerificationResult.Failed;
            }
        }

        return IsTarget(config)
            ? PasswordVerificationResult.Success
            : PasswordVerificationResult.SuccessRehashNeeded;
    }

    // argon2i, an older version, a lane count libsodium cannot read or weaker costs: the hash
    // is rewritten at the target. Stronger costs are kept.
    private static bool IsTarget(Argon2Config config) =>
        config.Type == Argon2Type.HybridAddressing
        && config.Version == Argon2Version.Nineteen
        && config.Lanes == Lanes
        && config.MemoryCost >= MemoryCostKib
        && config.TimeCost >= TimeCost;

    private static bool TryDecode(
        Argon2Config config,
        string encoded,
        out SecureArray<byte> expected)
    {
        try
        {
            var decoded = config.DecodeString(encoded, out var hash) && hash is not null;
            expected = hash!;
            return decoded;
        }
        catch (ArgumentException)
        {
            expected = null!;
            return false;
        }
    }

    // One thread whatever the lanes: the gate, not the hash, decides the parallelism.
    private static bool Matches(Argon2Config config, SecureArray<byte> expected)
    {
        config.Threads = 1;
        using var argon2 = new Argon2(config);
        using var actual = argon2.Hash();
        return Argon2.FixedTimeEquals(actual, expected);
    }
}
