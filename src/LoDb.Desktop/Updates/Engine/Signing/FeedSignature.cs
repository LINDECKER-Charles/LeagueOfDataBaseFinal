using System.Text;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace LoDb.Desktop.Updates.Engine.Signing;

/// <summary>
/// The Ed25519 signature (RFC 8032) of a release feed, checked against the one key the app
/// trusts (ADR 0008). The signed message is a context line, the feed's file name, then the
/// feed's bytes as published: <c>tools/desktop-update-signing</c> signs exactly this, and the
/// file name binds a signature to its channel, so a feed signed for one RID or channel never
/// passes for another's.
/// </summary>
internal sealed class FeedSignature
{
    private const string Context = "lodb-desktop-feed-v1";
    private const int SignatureLength = 64;

    private readonly Ed25519PublicKeyParameters _key;

    /// <param name="publicKey">The base64 of the key's 32 raw bytes.</param>
    /// <exception cref="FormatException">Not base64.</exception>
    /// <exception cref="ArgumentException">Not the encoding of an Ed25519 public key.</exception>
    public FeedSignature(string publicKey)
    {
        var raw = Convert.FromBase64String(publicKey);
        _key = new Ed25519PublicKeyParameters(raw);
        PublicKey = publicKey;
    }

    /// <summary>The trusted key, as the smoke report shows it.</summary>
    public string PublicKey { get; }

    /// <param name="feedName">The feed's file name, <c>releases.{channel}.json</c>.</param>
    /// <param name="feed">The feed's bytes as downloaded.</param>
    /// <param name="signature">The content of the signature file: the signature in base64.</param>
    public bool Holds(string feedName, byte[] feed, string signature)
    {
        var raw = new byte[SignatureLength];
        if (!Convert.TryFromBase64String(signature.Trim(), raw, out var length)
            || length != SignatureLength)
        {
            return false;
        }

        var verifier = new Ed25519Signer();
        verifier.Init(forSigning: false, _key);
        verifier.BlockUpdate(Encoding.UTF8.GetBytes($"{Context}\n{feedName}\n"));
        verifier.BlockUpdate(feed);
        return verifier.VerifySignature(raw);
    }
}
