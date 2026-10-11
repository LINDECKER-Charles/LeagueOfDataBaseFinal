using System.Text;
using LoDb.Desktop.Updates.Engine.Signing;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace LoDb.Desktop.Tests.Updates.Signing;

/// <summary>
/// A throwaway Ed25519 key, generated for each test, signing as the release tool does: no
/// key, not even a test one, lives in the repository.
/// </summary>
internal sealed class TestFeedKey
{
    private readonly Ed25519PrivateKeyParameters _key = new(new SecureRandom());

    public string PublicKey => Convert.ToBase64String(_key.GeneratePublicKey().GetEncoded());

    public FeedSignature Verifier() => new(PublicKey);

    /// <summary>The signature file of the feed: context line, file name, then its bytes.</summary>
    public string Sign(byte[] feed, string feedName = TestReleases.FeedName)
    {
        var signer = new Ed25519Signer();
        signer.Init(forSigning: true, _key);
        signer.BlockUpdate(Encoding.UTF8.GetBytes($"lodb-desktop-feed-v1\n{feedName}\n"));
        signer.BlockUpdate(feed);
        return Convert.ToBase64String(signer.GenerateSignature()) + "\n";
    }

    public FeedFile Signed(byte[] feed) => new()
    {
        Name = TestReleases.FeedName,
        Origin = "desktop-v-test",
        Content = feed,
        Signature = Sign(feed),
    };
}
