using System.Text.Json;
using System.Text.RegularExpressions;
using LoDb.Api.Modules.Admin.Prerender.Rendering;

namespace LoDb.Api.Modules.Admin.Prerender.Builds;

/// <summary>
/// The hashed files of the build the SSR runs, as <c>/_lodb/build-files</c> lists them:
/// <c>{"build":"&lt;sha256&gt;","files":["main-ABCD1234.js","media/x-ABCD1234.woff2"]}</c>.
/// </summary>
/// <remarks>
/// Each name becomes a key of the store (<c>build/{name}</c>) and a path asked of the SSR:
/// only a bundle's name or a media file's, a single segment below <c>media/</c> at most, is
/// taken.
/// </remarks>
internal sealed partial record BuildListing
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The digest of the sorted names, which the build's pages carry.</summary>
    public required string Build { get; init; }

    public required IReadOnlyList<string> Files { get; init; }

    /// <exception cref="SsrUnavailableException">The list is not one the SSR writes.</exception>
    public static BuildListing Parse(ReadOnlySpan<byte> json)
    {
        Document? document;
        try
        {
            document = JsonSerializer.Deserialize<Document>(json, Json);
        }
        catch (JsonException exception)
        {
            throw new SsrUnavailableException(
                "The SSR's list of its build is not JSON.",
                exception);
        }

        var isValid = document is { Build: { } build, Files: { Count: > 0 } files }
            && Digest().IsMatch(build)
            && files.All(static name => FileName().IsMatch(name));
        return isValid
            ? new BuildListing { Build = document!.Build!, Files = document.Files! }
            : throw new SsrUnavailableException("The SSR's list of its build is malformed.");
    }

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex Digest();

    [GeneratedRegex("^(media/)?[A-Za-z0-9_-][A-Za-z0-9._-]*$")]
    private static partial Regex FileName();

    private sealed record Document
    {
        public string? Build { get; init; }

        public IReadOnlyList<string>? Files { get; init; }
    }
}
