namespace LoDb.Desktop.Launch;

/// <summary>The release channels and the API each one talks to (ADR 0008).</summary>
internal static class DesktopChannels
{
    /// <summary>Releases of the production site.</summary>
    public const string Stable = "stable";

    /// <summary>Releases of the staging site, published under the <c>-beta</c> feeds.</summary>
    public const string Beta = "beta";

    private static readonly Uri ProductionApi = new("https://league-of-data-base.com");
    private static readonly Uri StagingApi = new("https://test.league-of-data-base.com");

    /// <exception cref="ArgumentException">The build names an unknown channel.</exception>
    public static Uri ApiOriginOf(string channel) => channel switch
    {
        Stable => ProductionApi,
        Beta => StagingApi,
        _ => throw new ArgumentException($"Unknown desktop channel '{channel}'.", nameof(channel)),
    };
}
