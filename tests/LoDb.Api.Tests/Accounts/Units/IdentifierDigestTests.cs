using LoDb.Api.Modules.Accounts.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.Accounts.Units;

/// <summary>
/// What the journal keeps of an identifier typed on a failed sign-in: its kind, null for
/// input that is neither an e-mail nor a username, and a keyed digest that matches the
/// identifier whatever its case and spaces without holding its text.
/// </summary>
public sealed class IdentifierDigestTests
{
    private const string Key = "identifier-key";

    [Theory]
    [InlineData(" Legende@Example.test ", IdentifierDigest.EmailKind)]
    [InlineData(" legende_42 ", IdentifierDigest.UsernameKind)]
    [InlineData("my password typed here!", null)]
    [InlineData(null, null)]
    public void KindIsOnlyTheShapeOfTheIdentifier(string? identifier, string? kind) =>
        Assert.Equal(kind, IdentifierDigest.KindOf(identifier));

    [Fact]
    public void DigestMatchesTheIdentifierWhateverItsCaseAndSpacesWithoutItsText()
    {
        var digest = Digest(Key).Of(" legende_42 ");

        Assert.Equal(Digest(Key).Of("LEGENDE_42"), digest);
        Assert.DoesNotContain("legende", digest, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(Digest("another-key").Of("LEGENDE_42"), digest);
    }

    private static IdentifierDigest Digest(string key) => new(
        Options.Create(new AuditIdentifierOptions { IdentifierKey = key }),
        NullLogger<IdentifierDigest>.Instance);
}
