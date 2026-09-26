using System.Text.RegularExpressions;

namespace LoDb.Infrastructure.Storage;

/// <summary>
/// Validation of the caller-supplied parts of a storage path.
/// </summary>
/// <remarks>
/// A segment starts with a letter or a digit and holds only letters, digits, <c>.</c>,
/// <c>_</c> and <c>-</c>: no separator, no drive, no <c>.</c> or <c>..</c>, and no hidden
/// name that could reach the staging area. A validated path is therefore always relative
/// and always below the root.
/// </remarks>
internal static partial class StoragePathSegments
{
    private const char Separator = '/';

    /// <summary>Checks one segment and returns it unchanged.</summary>
    public static string Require(string segment, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(segment, parameterName);
        if (!SegmentPattern().IsMatch(segment))
        {
            throw new ArgumentException(
                $"'{segment}' is not a valid storage path segment.",
                parameterName);
        }

        return segment;
    }

    /// <summary>Checks a <c>/</c>-separated relative path and returns it unchanged.</summary>
    public static string RequireRelativePath(string path, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(path, parameterName);
        foreach (var segment in path.Split(Separator))
        {
            Require(segment, parameterName);
        }

        return path;
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,99}\z", RegexOptions.CultureInvariant)]
    private static partial Regex SegmentPattern();
}
