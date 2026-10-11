using System.Security.Cryptography;
using System.Text;
using LoDb.Infrastructure.Persistence.Roles;

namespace LoDb.Infrastructure.Tests.Persistence.Roles;

/// <summary>
/// The verifier <c>migrate</c> sets as the API's password matches its own password only;
/// PostgreSQL's own verifiers are compared in <see cref="DatabaseRolesTests"/>.
/// </summary>
public sealed class ScramVerifierTests
{
    private const string Password = "0123456789abcdef-Pass word~";

    // The count ScramVerifier writes, PostgreSQL's default.
    private const int WrittenIterations = 4096;

    [Fact]
    public void VerifierMatchesItsPasswordOnly()
    {
        var verifier = ScramVerifier.Create(Password);

        Assert.StartsWith("SCRAM-SHA-256$4096:", verifier, StringComparison.Ordinal);
        Assert.True(ScramVerifier.Matches(Password, verifier));
        Assert.False(ScramVerifier.Matches(Password + "x", verifier));
    }

    [Fact]
    public void EachVerifierHasItsOwnSalt() =>
        Assert.NotEqual(ScramVerifier.Create(Password), ScramVerifier.Create(Password));

    [Fact]
    public void VerifierOfAnotherIterationCountIsNeverDerived()
    {
        Assert.True(ScramVerifier.Matches(Password, VerifierAt(WrittenIterations)));

        Assert.False(ScramVerifier.Matches(Password, VerifierAt(WrittenIterations + 1)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("md5a3556571e93b0d20722ba62be61e8c2d")]
    [InlineData("SCRAM-SHA-256$4096:c2FsdA==")]
    [InlineData("SCRAM-SHA-256$0:c2FsdA==$a2V5:a2V5")]
    [InlineData("SCRAM-SHA-256$2147483647:c2FsdA==$a2V5:a2V5")]
    [InlineData("SCRAM-SHA-256$4096:not base64$a2V5:a2V5")]
    [InlineData("SCRAM-SHA-1$4096:c2FsdA==$a2V5:a2V5")]
    public void AnythingElseNeverMatches(string? stored) =>
        Assert.False(ScramVerifier.Matches(Password, stored));

    // A verifier of the password at another count, as PostgreSQL stores one that a role sets
    // for itself with its own scram_iterations.
    private static string VerifierAt(int iterations)
    {
        byte[] salt = [.. "a-fixed-salt-16b"u8];
        var salted = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(Password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            SHA256.HashSizeInBytes);
        var storedKey = SHA256.HashData(HMACSHA256.HashData(salted, "Client Key"u8.ToArray()));
        var serverKey = HMACSHA256.HashData(salted, "Server Key"u8.ToArray());
        return $"SCRAM-SHA-256${iterations}:{Convert.ToBase64String(salt)}"
            + $"${Convert.ToBase64String(storedKey)}:{Convert.ToBase64String(serverKey)}";
    }
}
