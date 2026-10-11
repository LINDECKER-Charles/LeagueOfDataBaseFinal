using LoDb.Infrastructure.Persistence.Roles;

namespace LoDb.Infrastructure.Tests.Persistence.Roles;

/// <summary>
/// The verifier <c>migrate</c> sets as the API's password matches its own password only;
/// PostgreSQL's own verifiers are compared in <see cref="DatabaseRolesTests"/>.
/// </summary>
public sealed class ScramVerifierTests
{
    private const string Password = "0123456789abcdef-Pass word~";

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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("md5a3556571e93b0d20722ba62be61e8c2d")]
    [InlineData("SCRAM-SHA-256$4096:c2FsdA==")]
    [InlineData("SCRAM-SHA-256$0:c2FsdA==$a2V5:a2V5")]
    [InlineData("SCRAM-SHA-256$4096:not base64$a2V5:a2V5")]
    [InlineData("SCRAM-SHA-1$4096:c2FsdA==$a2V5:a2V5")]
    public void AnythingElseNeverMatches(string? stored) =>
        Assert.False(ScramVerifier.Matches(Password, stored));
}
