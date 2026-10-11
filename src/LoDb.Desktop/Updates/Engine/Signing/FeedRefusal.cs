namespace LoDb.Desktop.Updates.Engine.Signing;

/// <summary>Why a release feed is ignored: the app reads no feed it cannot trust.</summary>
internal enum FeedRefusal
{
    /// <summary>The release has no signature file: unsigned, or published before signing.</summary>
    MissingSignature,

    /// <summary>The signature does not hold: tampered, or signed with another key.</summary>
    InvalidSignature,

    /// <summary>Signed, yet not a feed Velopack can read.</summary>
    Unreadable,

    /// <summary>An entry lacks the size or SHA-256 that authenticates its package.</summary>
    IncompleteEntry,
}
