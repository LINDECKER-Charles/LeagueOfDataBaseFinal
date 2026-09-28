using System.Text.Json.Nodes;

namespace LoDb.Parity.Comparison;

/// <summary>
/// An image of the projection, <c>{ file, status, url }</c>: its verdict is what a page
/// shows, the stored blob or the placeholder.
/// </summary>
public static class ImageVerdict
{
    public const string Present = "present";

    public const string Absent = "absent";

    public const string Pending = "pending";

    public static bool IsImage(JsonObject node) =>
        node.ContainsKey("status") && node.ContainsKey("file");

    /// <summary><c>present /cdn/blobs/…</c>, <c>absent</c> or <c>pending</c>.</summary>
    public static string Of(JsonObject image)
    {
        var status = image["status"]?.GetValue<string>() ?? "null";
        return image["url"]?.GetValue<string>() is { } url ? $"{status} {url}" : status;
    }
}
