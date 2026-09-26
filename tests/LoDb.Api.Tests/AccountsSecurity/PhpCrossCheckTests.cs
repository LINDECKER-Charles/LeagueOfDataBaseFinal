using LoDb.Api.Modules.Accounts.Security.Hashing;
using LoDb.Infrastructure.Persistence.Accounts;

namespace LoDb.Api.Tests.AccountsSecurity;

/// <summary>
/// A hash written here stays readable by the legacy stack, so that a rollback signs nobody
/// out: PHP 8.5's <c>password_verify</c> and Symfony's check, which hands argon2 to
/// libsodium, accept it for every password of the fixtures, and refuse another password.
/// </summary>
public sealed class PhpCrossCheckTests(PhpContainer php) : IClassFixture<PhpContainer>
{
    private static readonly HashingGate Gate = new();

    private static readonly User Anyone = new() { Roles = [] };

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static MigratingPasswordHasher Hasher => new(new Argon2Passwords(Gate));

    [Fact]
    public async Task ContainerRunsThePhpOfTheFixtures() =>
        Assert.Equal(
            MajorAndMinor(PhpHashes.PhpVersion),
            MajorAndMinor(await php.VersionAsync(Cancellation)));

    [Fact]
    public async Task PhpVerifiesTheHashesWrittenHere()
    {
        var cases = PhpHashes.Passwords.Values
            .Select(static password => (password, Hasher.HashPassword(Anyone, password)))
            .ToList();

        var verdicts = await php.VerifyAsync(cases, Cancellation);

        Assert.Equal(Enumerable.Repeat((true, true), cases.Count), verdicts);
    }

    [Fact]
    public async Task PhpRefusesAnotherPassword()
    {
        var hash = Hasher.HashPassword(Anyone, PhpHashes.Passwords["ascii"]);

        var verdicts = await php.VerifyAsync(
            [(PhpHashes.Passwords["accents"], hash)],
            Cancellation);

        Assert.Equal([(false, false)], verdicts);
    }

    private static string MajorAndMinor(string version) => Version.Parse(version).ToString(2);
}
