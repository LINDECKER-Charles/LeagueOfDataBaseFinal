using LoDb.Api.Modules.Accounts.Security.Hashing;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;
using BCryptHashes = BCrypt.Net.BCrypt;

namespace LoDb.Api.Tests.AccountsSecurity;

/// <summary>
/// New hashes are argon2id at the target, salted each time. Symfony's bounds hold both ways:
/// an empty password or one over 4096 bytes is neither hashed nor matched, and a NUL is part
/// of the password. A hash of an unknown or broken format fails, never throws.
/// </summary>
public sealed class MigratingPasswordHasherTests
{
    /// <summary>The PHC string of the target: 16 bytes of salt, 32 of hash, unpadded.</summary>
    internal const string TargetFormat =
        @"^\$argon2id\$v=19\$m=19456,t=2,p=1\$[A-Za-z0-9+/]{22}\$[A-Za-z0-9+/]{43}$";

    private const string Password = "Corr3ct-horse-Battery!";
    private const string Nul = "Before\0After-2026";

    // bcrypt's lowest cost, for a hash made in the test.
    private const int QuickBcryptCost = 4;

    // What a column too short, or a copy cut short, drops from the end of a hash.
    private const int CutCharacters = 10;

    // 'é' takes two bytes of UTF-8.
    private const int AccentBytes = 2;

    private static readonly HashingGate Gate = new();

    private static readonly User Anyone = new() { Roles = [] };

    private static readonly string TooLong = new('a', PasswordInput.MaxBytes + 1);

    public static TheoryData<string> OutOfBounds =>
    [
        string.Empty,
        TooLong,
        // 2049 characters, but 4098 bytes: the bound is in bytes, as PHP's strlen counts.
        new string('é', (PasswordInput.MaxBytes / AccentBytes) + 1),
    ];

    public static TheoryData<string> Malformed =>
    [
        string.Empty,
        Password,
        "$1$saltsalt$qjXMvbEw8oaL.CzflDugX/",
        "$5$rounds=5000$saltsalt$bmeqcHZ5mSxNpP6kxK2p5LnaWFW0iuR.eKhE8zfSyE2",
        "$2y$13$Sx6YwqOwrFl0CEL.fFj39e",
        "$2y$99$Sx6YwqOwrFl0CEL.fFj39eeAXJTyP2wc1aoL5t5oqW/2gf2pN6VIC",
        "$argon2id$v=19$m=19456,t=2,p=1$",
        "$argon2id$v=19$m=19456,t=2,p=1$!!!$###",
        "$argon2id$v=19$m=lots,t=2,p=1$c2FsdHNhbHRzYWx0c2FsdA$aGFzaGhhc2hoYXNoaGFzaA",
        "$argon2id$v=99$m=19456,t=2,p=1$c2FsdHNhbHRzYWx0c2FsdA$aGFzaGhhc2hoYXNoaGFzaA",
    ];

    private static MigratingPasswordHasher Hasher => new(new Argon2Passwords(Gate));

    [Fact]
    public void NewHashIsArgon2idAtTheTarget() =>
        Assert.Matches(TargetFormat, Hasher.HashPassword(Anyone, Password));

    [Fact]
    public void EachHashHasItsSaltAndVerifiesWithoutRehash()
    {
        var first = Hasher.HashPassword(Anyone, Password);
        var second = Hasher.HashPassword(Anyone, Password);

        Assert.NotEqual(first, second);
        Assert.Equal(
            [PasswordVerificationResult.Success, PasswordVerificationResult.Success],
            [Verify(first, Password), Verify(second, Password)]);
    }

    [Fact]
    public void AnotherPasswordFails() =>
        Assert.Equal(
            PasswordVerificationResult.Failed,
            Verify(Hasher.HashPassword(Anyone, Password), Password.ToUpperInvariant()));

    [Theory]
    [MemberData(nameof(OutOfBounds))]
    public void PasswordOutsideSymfonysBoundsIsNotHashed(string password) =>
        Assert.Throws<ArgumentException>(() => Hasher.HashPassword(Anyone, password));

    // Hashes made apart, which do match: the bounds, not the hash, refuse the password.
    [Fact]
    public void PasswordOutsideSymfonysBoundsNeverMatches() =>
        Assert.Equal(
            [PasswordVerificationResult.Failed, PasswordVerificationResult.Failed],
            [
                Verify(BCryptHashes.HashPassword(string.Empty, QuickBcryptCost), string.Empty),
                Verify(Argon2Hashes.Make(TooLong), TooLong),
            ]);

    [Fact]
    public void PasswordOfExactlyTheMaximumIsHashed()
    {
        var password = new string('é', PasswordInput.MaxBytes / AccentBytes);

        Assert.Equal(
            PasswordVerificationResult.Success,
            Verify(Hasher.HashPassword(Anyone, password), password));
    }

    [Fact]
    public void NulIsPartOfThePassword()
    {
        var hash = Hasher.HashPassword(Anyone, Nul);

        Assert.Equal(
            [PasswordVerificationResult.Success, PasswordVerificationResult.Failed],
            [Verify(hash, Nul), Verify(hash, Nul[..Nul.IndexOf('\0')])]);
    }

    [Theory]
    [MemberData(nameof(Malformed))]
    public void HashOfAnUnknownOrBrokenFormatFails(string hash) =>
        Assert.Equal(PasswordVerificationResult.Failed, Verify(hash, Password));

    [Fact]
    public void CutHashFails()
    {
        var hash = Hasher.HashPassword(Anyone, Password);

        Assert.Equal(PasswordVerificationResult.Failed, Verify(hash[..^CutCharacters], Password));
    }

    private static PasswordVerificationResult Verify(string hash, string password) =>
        Hasher.VerifyHashedPassword(Anyone, hash, password);
}
