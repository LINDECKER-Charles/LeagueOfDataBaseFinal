using System.Buffers;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace LoDb.Desktop.Bridge.Validation;

/// <summary>
/// A file name the page may suggest to the save dialog: a plain name that every system
/// accepts, never a path. The user still picks the folder, and may rename the file.
/// </summary>
internal static class SaveFileName
{
    /// <summary>Longest name accepted, the limit of the common file systems.</summary>
    public const int MaxLength = 255;

    private const char ExtensionSeparator = '.';

    // Path separators, plus the characters Windows refuses in a name.
    private static readonly SearchValues<char> Forbidden = SearchValues.Create("/\\<>:\"|?*");

    // Windows opens a device, not a file, for these names, whatever the extension.
    private static readonly FrozenSet<string> ReservedDeviceNames = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9");

    public static bool IsValid([NotNullWhen(true)] string? name) =>
        name is { Length: > 0 and <= MaxLength }
        && HasPlainEdges(name)
        && name.AsSpan().IndexOfAny(Forbidden) < 0
        && !name.Any(IsInvisible)
        && !IsReservedDeviceName(name);

    // A leading dot hides the file (and covers "." and ".."); Windows drops trailing dots
    // and spaces, so the saved name would differ from the one shown.
    private static bool HasPlainEdges(string name) =>
        name[0] != ExtensionSeparator
        && !char.IsWhiteSpace(name[0])
        && name[^1] != ExtensionSeparator
        && !char.IsWhiteSpace(name[^1]);

    // Control characters, and format characters such as the right-to-left override, which
    // disguises an extension ("invoice{U+202E}txt.exe").
    private static bool IsInvisible(char character) =>
        char.IsControl(character)
        || char.GetUnicodeCategory(character) == UnicodeCategory.Format;

    private static bool IsReservedDeviceName(string name)
    {
        var separator = name.IndexOf(ExtensionSeparator, StringComparison.Ordinal);
        var stem = separator < 0 ? name : name[..separator];
        return ReservedDeviceNames.Contains(stem.TrimEnd());
    }
}
