namespace LoDb.Desktop.Updates;

/// <summary>What the command line and the environment say about updates, for one run.</summary>
internal sealed record UpdateSettings
{
    public required UpdateMode Mode { get; init; }

    /// <summary>
    /// A local release folder read instead of GitHub Releases, as the value of
    /// <see cref="UpdateArguments.LocalFeedVariable"/>; null in normal use. Unvalidated:
    /// <c>UpdateFeeds</c> accepts an absolute folder path only.
    /// </summary>
    public string? LocalFeed { get; init; }
}
