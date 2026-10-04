using LoDb.Api.Modules.Accounts.Security.Hashing;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Tests.AccountsSecurity;

/// <summary>
/// Every hash the legacy stack writes, made by PHP 8.5 itself, verifies here: the ones below
/// the target ask for a rehash, the stronger argon2id ones are kept as they are.
/// </summary>
public sealed class PhpHashesTests
{
    private const string Ascii = "ascii";
    private const string Long = "long";
    private const string Nul = "nul";

    private static readonly HashingGate Gate = new();

    private static readonly User Anyone = new() { Roles = [] };

    // The formats of generate.php and what the hasher answers for each.
    private static readonly Dictionary<string, PasswordVerificationResult> Expected = new()
    {
        ["bcrypt"] = PasswordVerificationResult.SuccessRehashNeeded,
        ["bcrypt-cost4"] = PasswordVerificationResult.SuccessRehashNeeded,
        ["bcrypt-2a"] = PasswordVerificationResult.SuccessRehashNeeded,
        ["argon2id-sodium"] = PasswordVerificationResult.Success,
        ["argon2id"] = PasswordVerificationResult.Success,
        ["argon2i"] = PasswordVerificationResult.SuccessRehashNeeded,
        ["argon2id-weak"] = PasswordVerificationResult.SuccessRehashNeeded,
        ["argon2id-lanes2"] = PasswordVerificationResult.SuccessRehashNeeded,
    };

    public static TheoryData<string, string> Cases => new(PhpHashes.Cases);

    public static TheoryData<string> Formats => new(PhpHashes.Formats);

    [Fact]
    public void FixturesAreThoseOfPhp85AndCoverEveryFormat()
    {
        Assert.StartsWith("8.5.", PhpHashes.PhpVersion, StringComparison.Ordinal);
        Assert.Equal(
            Expected.Keys.Order(StringComparer.Ordinal),
            PhpHashes.Formats.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void HashOfThePhpStackVerifies(string format, string password) =>
        Assert.Equal(
            Expected[format],
            Verify(PhpHashes.Hash(format, password), PhpHashes.Passwords[password]));

    [Theory]
    [MemberData(nameof(Formats))]
    public void AnotherPasswordFails(string format) =>
        Assert.Equal(
            PasswordVerificationResult.Failed,
            Verify(PhpHashes.Hash(format, Ascii), PhpHashes.Passwords[Ascii] + "?"));

    // bcrypt alone reads 72 bytes: without Symfony's SHA-512 first, any password sharing
    // them would match.
    [Fact]
    public void LongPasswordsDifferPastTheSeventyTwoBytesOfBcrypt() =>
        Assert.Equal(
            PasswordVerificationResult.Failed,
            Verify(PhpHashes.Hash("bcrypt", Long), PhpHashes.Passwords[Long] + "!"));

    // bcrypt alone stops at a NUL: the password must not end there.
    [Fact]
    public void PasswordsDifferPastANul()
    {
        var password = PhpHashes.Passwords[Nul];

        Assert.Equal(
            PasswordVerificationResult.Failed,
            Verify(PhpHashes.Hash("bcrypt", Nul), password[..password.IndexOf('\0')]));
    }

    private static PasswordVerificationResult Verify(string hash, string password) =>
        new MigratingPasswordHasher(new Argon2Passwords(Gate))
            .VerifyHashedPassword(Anyone, hash, password);
}
