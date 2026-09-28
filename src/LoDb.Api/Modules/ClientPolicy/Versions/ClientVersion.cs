using System.Diagnostics.CodeAnalysis;

namespace LoDb.Api.Modules.ClientPolicy.Versions;

/// <summary>
/// The version an app states in <c>X-LoDb-Client</c>: a release number, and for a beta of
/// the staging channel a pre-release suffix (<c>1.4.0-beta.3</c>).
/// </summary>
/// <remarks>
/// The policy only publishes release numbers, so a version is only ever compared with a
/// release; as in SemVer, a pre-release comes before its release.
/// </remarks>
internal sealed class ClientVersion
{
    // Longer than any real tag: a header is untrusted input.
    private const int MaxLength = 64;
    private const char PreReleaseSeparator = '-';
    private const char IdentifierSeparator = '.';

    private ClientVersion(Version release, string preRelease)
    {
        Release = release;
        PreRelease = preRelease;
    }

    public Version Release { get; }

    /// <summary>What follows the dash, such as <c>beta.3</c>; empty for a release.</summary>
    public string PreRelease { get; }

    public static bool TryParse(string? text, [NotNullWhen(true)] out ClientVersion? version)
    {
        version = null;
        if (text is null || text.Length > MaxLength)
        {
            return false;
        }

        var dash = text.IndexOf(PreReleaseSeparator, StringComparison.Ordinal);
        var core = dash < 0 ? text : text[..dash];
        var suffix = dash < 0 ? string.Empty : text[(dash + 1)..];
        if (!AppVersion.TryParse(core, out var release) || (dash >= 0 && !IsPreRelease(suffix)))
        {
            return false;
        }

        version = new ClientVersion(release, suffix);
        return true;
    }

    /// <summary>Whether this version is older than <paramref name="minimum"/>.</summary>
    public bool IsBelow(Version minimum)
    {
        ArgumentNullException.ThrowIfNull(minimum);
        var byRelease = Release.CompareTo(minimum);
        return byRelease < 0 || (byRelease == 0 && PreRelease.Length > 0);
    }

    public override string ToString() => PreRelease.Length == 0
        ? Release.ToString()
        : $"{Release}{PreReleaseSeparator}{PreRelease}";

    // SemVer: dot-separated, non-empty identifiers of ASCII letters, digits and hyphens.
    private static bool IsPreRelease(string suffix) =>
        suffix.Split(IdentifierSeparator).All(static identifier =>
            identifier.Length > 0 && identifier.All(IsIdentifierCharacter));

    private static bool IsIdentifierCharacter(char c) =>
        char.IsAsciiLetterOrDigit(c) || c == PreReleaseSeparator;
}
