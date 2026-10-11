using System.Text;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace LoDb.Desktop.Updates.Engine.Signing;

/// <summary>
/// The Ed25519 signature (RFC 8032) of a release feed, checked against the keys the app
/// trusts (ADR 0008). The signed message is a context line, the feed's file name, then the
/// feed's bytes as published: <c>tools/desktop-update-signing</c> signs exactly this, and the
/// file name binds a signature to its channel, so a feed signed for one RID or channel never
/// passes for another's.
/// </summary>
internal sealed class FeedSignature
{
    private const string Context = "lodb-desktop-feed-v1";
    private const int SignatureLength = 64;

    private readonly Ed25519PublicKeyParameters[] _keys;

    /// <param name="publicKeys">Each key as the base64 of its 32 raw bytes.</param>
    /// <exception cref="FormatException">A key is not base64.</exception>
    /// <exception cref="ArgumentException">
    /// No key, or one that does not encode an Ed25519 public key.
    /// </exception>
    public FeedSignature(IReadOnlyList<string> publicKeys)
    {
        if (publicKeys.Count == 0)
        {
            throw new ArgumentException("The app must trust a feed key.", nameof(publicKeys));
        }

        _keys = [.. publicKeys.Select(key => new Ed25519PublicKeyParameters(
            Convert.FromBase64String(key)))];
        PublicKeys = publicKeys;
    }

    /// <summary>The trusted keys, as the smoke report shows them.</summary>
    public IReadOnlyList<string> PublicKeys { get; }

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

        // A feed is a few kilobytes: one copy is simpler than streaming it into each verifier.
        byte[] message = [.. Encoding.UTF8.GetBytes($"{Context}\n{feedName}\n"), .. feed];
        return _keys.Any(key => HoldsFor(key, message, raw));
    }

    private static bool HoldsFor(Ed25519PublicKeyParameters key, byte[] message, byte[] signature)
    {
        var verifier = new Ed25519Signer();
        verifier.Init(forSigning: false, key);
        verifier.BlockUpdate(message);
        return verifier.VerifySignature(signature);
    }
}
