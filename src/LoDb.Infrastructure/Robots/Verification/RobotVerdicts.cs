namespace LoDb.Infrastructure.Robots.Verification;

/// <summary>What the reverse DNS of a hit says of the robot its user agent names.</summary>
public static class RobotVerdicts
{
    /// <summary>
    /// Its address reverses under its operator's domains and resolves back to it.
    /// </summary>
    public const string Verified = "verified";

    /// <summary>
    /// Its operator documents its domains, and its address reverses elsewhere or nowhere: the
    /// user agent most likely borrows the robot's name.
    /// </summary>
    public const string Mismatch = "mismatch";

    /// <summary>The lookup failed or was put off: nothing can be told yet.</summary>
    public const string Unresolved = "unresolved";

    /// <summary>Its operator documents no domain to check it against.</summary>
    public const string Unverifiable = "unverifiable";

    public static IReadOnlyList<string> All { get; } =
        [Verified, Mismatch, Unresolved, Unverifiable];
}
