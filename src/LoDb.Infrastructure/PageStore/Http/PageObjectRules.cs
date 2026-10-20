using System.Text;
using System.Text.RegularExpressions;
using LoDb.Infrastructure.PageStore.Errors;

namespace LoDb.Infrastructure.PageStore.Http;

/// <summary>
/// What an object must be before it is sent: a key the HTTP stack cannot rewrite, header
/// values that fit on one line, user metadata within the S3 limit.
/// </summary>
/// <remarks>
/// A bad key or metadata name is the caller's bug (<see cref="ArgumentException"/>); a value
/// taken from a rendered page that cannot be stored is a refusal the pass counts.
/// </remarks>
internal static partial class PageObjectRules
{
    /// <summary>
    /// The S3 limit on user metadata, counted the strict way: the bytes of every name with its
    /// <c>x-amz-meta-</c> prefix, and of every value.
    /// </summary>
    /// <remarks>
    /// AWS counts names without the prefix; how Scaleway counts is unproven, so the budget
    /// assumes the prefix counts until <c>tools/prerender</c> verifies it.
    /// </remarks>
    public const int MaxMetadataBytes = 2 * 1024;

    private const int MaxKeyBytes = 1024;
    private const char Separator = '/';
    private const string CurrentSegment = ".";
    private const string ParentSegment = "..";
    private const char Tab = '\t';
    private const char FirstPrintable = ' ';
    private const char LastPrintable = '~';

    /// <summary>Throws unless <paramref name="key"/> reaches the store unchanged.</summary>
    public static void CheckKey(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        var isMalformed = key.Length > MaxKeyBytes
            || Encoding.UTF8.GetByteCount(key) > MaxKeyBytes
            || key.Any(char.IsControl)
            || key.Split(Separator).Any(static segment =>
                segment.Length == 0 || segment is CurrentSegment or ParentSegment);
        if (isMalformed)
        {
            // The HTTP stack would merge empty segments and resolve dot segments.
            throw new ArgumentException(
                "A key is a relative path of non-empty segments, without dot segments.",
                nameof(key));
        }
    }

    /// <summary>Throws unless <paramref name="page"/> can be written as it is.</summary>
    public static void Check(PageObject page)
    {
        CheckKey(page.Key);
        foreach (var name in page.Metadata.Keys.Where(static name => !MetadataName().IsMatch(name)))
        {
            throw new ArgumentException(
                $"Metadata name '{name}' is not lower-case letters, digits and hyphens.",
                nameof(page));
        }

        string?[] values =
            [page.ContentType, page.ContentEncoding, page.CacheControl, .. page.Metadata.Values];
        if (values.Any(static value => value is not null && !IsHeaderValue(value)))
        {
            throw new PageStoreRefusedException(
                $"A header of {page.Key} is not printable ASCII on one line.");
        }

        // ASCII by now: a character is a byte.
        var metadataBytes = page.Metadata.Sum(static pair =>
            PageStoreRequests.MetadataPrefix.Length + pair.Key.Length + pair.Value.Length);
        if (metadataBytes > MaxMetadataBytes)
        {
            throw new PageStoreRefusedException(
                $"The metadata of {page.Key} weigh {metadataBytes} bytes, "
                    + $"over {MaxMetadataBytes}.");
        }
    }

    // Printable ASCII and tabs: one byte per character, no line break to split the header.
    private static bool IsHeaderValue(string value) =>
        value.Length > 0
        && value.All(static character =>
            character is Tab or (>= FirstPrintable and <= LastPrintable));

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex MetadataName();
}
