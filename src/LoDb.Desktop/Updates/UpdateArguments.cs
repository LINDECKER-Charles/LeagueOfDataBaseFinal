using LoDb.Desktop.Launch;

namespace LoDb.Desktop.Updates;

/// <summary>
/// Reads the update settings of a run. <see cref="DesktopArguments"/> ignores the flags it
/// does not know, so the hidden flag of the update E2E is read here, from the same command
/// line.
/// </summary>
internal static class UpdateArguments
{
    /// <summary>
    /// Hidden flag of the update E2E: with <c>--smoke</c>, the run checks the feed, downloads
    /// the newer version, and Velopack applies it once the process has exited.
    /// </summary>
    public const string ApplyUpdates = "--apply-updates";

    /// <summary>
    /// A local release folder that replaces GitHub Releases: the E2E gate points an installed
    /// N-1 at the packages of N before they are published.
    /// </summary>
    public const string LocalFeedVariable = "LODB_DESKTOP_UPDATE_SOURCE";

    public static UpdateSettings Parse(
        IReadOnlyCollection<string> args,
        Func<string, string?> environment)
    {
        var localFeed = environment(LocalFeedVariable);
        return new UpdateSettings
        {
            Mode = ModeOf(args),
            LocalFeed = string.IsNullOrWhiteSpace(localFeed) ? null : localFeed,
        };
    }

    private static UpdateMode ModeOf(IReadOnlyCollection<string> args)
    {
        if (!args.Contains(DesktopArguments.Smoke, StringComparer.Ordinal))
        {
            return UpdateMode.Background;
        }

        return args.Contains(ApplyUpdates, StringComparer.Ordinal)
            ? UpdateMode.ApplyAtExit
            : UpdateMode.Off;
    }
}
