namespace LoDb.Desktop.Bridge;

/// <summary>The <c>error</c> codes of a bridge reply <c>{id, ok: false, error}</c>.</summary>
internal static class BridgeErrors
{
    /// <summary>No string <c>type</c>, or a <c>payload</c> that is not an object.</summary>
    public const string InvalidMessage = "invalid-message";

    public const string UnknownType = "unknown-type";

    /// <summary>Not an absolute http(s) URL of a remote host.</summary>
    public const string InvalidUrl = "invalid-url";

    /// <summary>
    /// A file name with a path, a reserved name, or a character that a system refuses.
    /// </summary>
    public const string InvalidName = "invalid-name";

    public const string InvalidMime = "invalid-mime";

    /// <summary>The <c>base64</c> field does not decode.</summary>
    public const string InvalidContent = "invalid-content";

    /// <summary>The file exceeds what the bridge carries.</summary>
    public const string TooLarge = "too-large";

    /// <summary>A save dialog is already open.</summary>
    public const string Busy = "busy";

    public const string WriteFailed = "write-failed";

    public const string BrowserFailed = "browser-failed";

    /// <summary><c>applyUpdate</c> without a downloaded update.</summary>
    public const string NoUpdate = "no-update";

    /// <summary>An unexpected failure of the host, logged there.</summary>
    public const string Internal = "internal";
}
