namespace LoDb.Desktop.Updates.Engine.Signing;

/// <summary>
/// A release feed as a source read it, before anything trusts it: its file name, the release
/// it comes from (for the logs), its bytes and its signature file.
/// </summary>
internal sealed record FeedFile
{
    /// <summary>Appended to the feed's file name to name its signature file.</summary>
    public const string SignatureSuffix = ".sig";

    /// <summary><c>releases.{channel}.json</c>, as Velopack names the feed of a channel.</summary>
    public required string Name { get; init; }

    /// <summary>The GitHub Release, or the local folder, the feed was read from.</summary>
    public required string Origin { get; init; }

    public required byte[] Content { get; init; }

    /// <summary>The content of the signature file; null when the release has none.</summary>
    public string? Signature { get; init; }

    /// <summary>The feed's file name for a Velopack channel (<c>win-x64</c>, …).</summary>
    public static string NameFor(string channel) => $"releases.{channel}.json";
}
