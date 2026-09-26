using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace LoDb.Domain.Versions;

/// <summary>
/// A Data Dragon patch version ("16.14.1", "0.151.2"), ordered segment by segment as numbers.
/// </summary>
/// <remarks>
/// The version string is the identity: it names the datasets, the URLs and the manifest rows,
/// so it is kept verbatim. Only well-formed dotted numbers parse, which is what rules out the
/// legacy <c>lolpatch_*</c> entries of <c>versions.json</c> (UP 4). No release date is
/// attached: Data Dragon publishes none, so nothing downstream can claim one (UP 13).
/// </remarks>
public sealed partial record PatchVersion : IComparable<PatchVersion>
{
    /// <summary>
    /// Shape of a version for the API contract and the front: dotted numbers, two segments at
    /// least. Digits are ASCII only, as in the parser below.
    /// </summary>
    public const string Pattern = @"\d+(?:\.\d+)+";

    private const char SegmentSeparator = '.';
    private const char LeadingZero = '0';

    private PatchVersion(string value) => Value = value;

    /// <summary>The version as Data Dragon writes it.</summary>
    public string Value { get; }

    public static bool TryParse(
        [NotNullWhen(true)] string? text,
        [NotNullWhen(true)] out PatchVersion? version)
    {
        version = text is not null && WellFormed().IsMatch(text) ? new PatchVersion(text) : null;
        return version is not null;
    }

    public static PatchVersion Parse(string text) =>
        TryParse(text, out var version)
            ? version
            : throw new FormatException($"'{text}' is not a Data Dragon version.");

    public int CompareTo(PatchVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        // Numerically equal spellings ("7.02" and "7.2") still differ as identities, so the
        // ordinal tie-break keeps the order consistent with equality.
        var numeric = CompareNumerically(Value, other.Value);
        return numeric != 0 ? numeric : string.CompareOrdinal(Value, other.Value);
    }

    public override string ToString() => Value;

    public static bool operator <(PatchVersion? left, PatchVersion? right) =>
        Compare(left, right) < 0;

    public static bool operator <=(PatchVersion? left, PatchVersion? right) =>
        Compare(left, right) <= 0;

    public static bool operator >(PatchVersion? left, PatchVersion? right) =>
        Compare(left, right) > 0;

    public static bool operator >=(PatchVersion? left, PatchVersion? right) =>
        Compare(left, right) >= 0;

    private static int Compare(PatchVersion? left, PatchVersion? right) =>
        left?.CompareTo(right) ?? (right is null ? 0 : -1);

    private static int CompareNumerically(string left, string right)
    {
        var leftSegments = left.Split(SegmentSeparator);
        var rightSegments = right.Split(SegmentSeparator);
        var shared = Math.Min(leftSegments.Length, rightSegments.Length);
        for (var index = 0; index < shared; index++)
        {
            var order = CompareSegment(leftSegments[index], rightSegments[index]);
            if (order != 0)
            {
                return order;
            }
        }

        // "15.1" precedes "15.1.1": the longer version refines the shorter one.
        return leftSegments.Length.CompareTo(rightSegments.Length);
    }

    // Compares digit strings without converting them, so no segment can overflow.
    private static int CompareSegment(string left, string right)
    {
        var leftDigits = left.TrimStart(LeadingZero);
        var rightDigits = right.TrimStart(LeadingZero);
        return leftDigits.Length != rightDigits.Length
            ? leftDigits.Length.CompareTo(rightDigits.Length)
            : string.CompareOrdinal(leftDigits, rightDigits);
    }

    // [0-9] and \z, not \d and $: .NET's \d matches every Unicode digit and $ accepts a
    // trailing newline, and neither belongs in a path segment.
    [GeneratedRegex(@"^[0-9]+(?:\.[0-9]+)+\z")]
    private static partial Regex WellFormed();
}
